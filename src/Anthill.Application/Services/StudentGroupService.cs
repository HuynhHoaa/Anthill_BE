using Anthill.Application.Common;
using Anthill.Application.DTOs.ExamAssignments;
using Anthill.Application.DTOs.StudentGroups;
using Anthill.Application.DTOs.Students;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

/// <summary>
/// Cài đặt <see cref="IStudentGroupService"/> — chín endpoint của §4.4.
/// </summary>
/// <remarks>
/// Bốn điều quyết định nằm ở file này:
/// <list type="number">
/// <item><b>Chủ sở hữu lấy từ JWT.</b> Không endpoint nào nhận <c>ownerTeacherId</c> từ client, nên
/// không có đường nào để giáo viên A tạo nhóm hộ giáo viên B hoặc cướp nhóm của B (§4.4).</item>
/// <item><b>Chạm vào nhóm người khác trả <c>NotFound</c>, không phải <c>Forbidden</c>.</b> Trả 403
/// là xác nhận id đó có thật, đủ để dò dần danh sách lớp của đồng nghiệp (§4.2).</item>
/// <item><b>Đổi tên nhóm không lan xuống bản ghi học sinh.</b> Học sinh chỉ giữ dòng thành viên
/// (<see cref="StudentGroupMember"/>); tên đọc qua JOIN. Vòng lặp <c>syncGroupCounts()</c> và đoạn cập nhật
/// <c>student.group</c> của frontend biến mất hoàn toàn ở đây.</item>
/// <item><b>Xoá nhóm không xoá tài khoản.</b> Chỉ nhóm rỗng mới xoá được, nên không có tình huống
/// "xoá lớp mất luôn học sinh" ngay từ đầu.</item>
/// </list>
/// </remarks>
public sealed class StudentGroupService : IStudentGroupService
{
    private const string NotFoundMessage = "Không tìm thấy nhóm lớp.";
    private const string StudentNotFoundMessage = "Không tìm thấy học sinh.";
    private const string TargetNotFoundMessage = "Không tìm thấy lớp muốn chuyển sang.";

    private const string LockedGroupMessage =
        "Lớp đã khoá nên chỉ xem được. Hãy mở khoá lớp trước khi thay đổi.";

    private static string LockedTargetMessage(StudentGroup target) =>
        $"Lớp {target.Name} đã khoá nên không xếp thêm em vào được. Hãy mở khoá lớp đó trước.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public StudentGroupService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<StudentGroupDto>>> ListAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default)
    {
        // Từ chối giá trị lạ thay vì âm thầm bỏ lọc: gõ sai một chữ mà vẫn nhận về đủ danh sách
        // thì người dùng tin là mình đã lọc, trong khi không có bộ lọc nào chạy cả.
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();
        if (normalizedStatus is not ("all" or "draft" or "published" or "locked"))
        {
            return Result.Failure<IReadOnlyList<StudentGroupDto>>(
                "Bộ lọc trạng thái chỉ nhận 'draft', 'published', 'locked' hoặc 'all'.");
        }

        // Hai đường đi tách hẳn nhau thay vì một truy vấn có tham số "phạm vi". Quản trị viên và
        // giáo viên nhìn hai tập dữ liệu khác nhau về bản chất — một bên là mọi lớp kể cả bản nháp,
        // bên kia là những lớp mình được phân công và đã công bố. Gộp vào một hàm với một cờ điều
        // kiện là cách để một ngày nào đó truyền nhầm cờ và làm lộ toàn bộ lớp đang dựng dở.
        IReadOnlyList<StudentGroup> groups;

        if (_currentUser.IsInRole(UserRole.Admin))
        {
            groups = await _unitOfWork.StudentGroups.GetAllAsync(
                search,
                includeDrafts: normalizedStatus != "published",
                cancellationToken);

            if (normalizedStatus == "draft")
            {
                groups = groups.Where(group => !group.IsPublished).ToList();
            }
            else if (normalizedStatus == "locked")
            {
                groups = groups.Where(group => group.IsLocked).ToList();
            }
        }
        else
        {
            // Giáo viên không bao giờ nhận về bản nháp nên tham số status không đổi được gì —
            // repository đã lọc sẵn ở tầng truy vấn.
            groups = await _unitOfWork.StudentGroups.GetForTeacherAsync(CurrentUserId, search, cancellationToken);
        }

        return Result.Success(await AttachCountsAsync(groups, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<StudentGroupDto>> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var group = await LoadReadableAsync(id, cancellationToken);
        if (group is null)
        {
            return Result.NotFound<StudentGroupDto>(NotFoundMessage);
        }

        var count = await _unitOfWork.Users.CountStudentsInGroupAsync(group.Id, cancellationToken);

        return Result.Success(group.ToDto(count));
    }

    /// <inheritdoc />
    public async Task<Result<StudentGroupDto>> CreateAsync(
        CreateStudentGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<StudentGroupDto>(AdminOnlyMessage);
        }

        var name = request.Name?.Trim() ?? string.Empty;

        // So trùng trên TOÀN hệ thống, không còn trong phạm vi một giáo viên. Lớp giờ là đơn vị
        // hành chính của cả trung tâm: hai lớp cùng tên "12A1" là hai thứ không ai phân biệt nổi
        // lúc giao bài, mà học sinh thì chỉ thuộc được một trong hai.
        if (await _unitOfWork.StudentGroups.IsNameTakenAsync(name, null, cancellationToken))
        {
            return Result.Conflict<StudentGroupDto>("Tên lớp đã tồn tại.");
        }

        // Mỗi lớp có đúng một giáo viên phụ trách, chọn ngay lúc lập lớp: không có lớp "chưa ai dạy" để rồi quên.
        var teacher = await ResolveTeacherAsync(request.TeacherId, cancellationToken);
        if (teacher.IsFailure)
        {
            return Result.Failure<StudentGroupDto>(teacher.Error!, teacher.ErrorCode);
        }

        var term = await ResolveTermAsync(request.TermId, cancellationToken);
        if (term.IsFailure)
        {
            return Result.Failure<StudentGroupDto>(term.Error!, term.ErrorCode);
        }

        var group = new StudentGroup
        {
            Name = name,
            Description = NormalizeOptional(request.Description),
            CreatedByUserId = CurrentUserId,
            AcademicTermId = term.ValueOrThrow?.Id,

            // Lớp mới luôn là bản nháp. Quản trị viên còn phải xếp học sinh trước khi nó có nghĩa gì;
            // hiện ngay lúc vừa tạo thì giáo viên thấy một lớp rỗng và giao bài cho nó cũng chẳng tới ai.
            IsPublished = false
        };

        // Lớp và giáo viên vào cùng một lần lưu: không có khoảnh khắc nào lớp tồn tại mà chưa có người phụ trách.
        group.Teachers.Add(new StudentGroupTeacher
        {
            TeacherId = teacher.ValueOrThrow.Id,
            AssignedAt = DateTime.UtcNow
        });

        await _unitOfWork.StudentGroups.AddAsync(group, cancellationToken);

        // Dòng lịch sử đầu tiên của lớp: ra đời với ai. Gắn qua navigation nên khoá ngoại tự điền khi lớp được lưu.
        await _unitOfWork.GroupChanges.AddAsync(
            new GroupChange
            {
                Group = group,
                Kind = GroupChangeKind.Created,
                ToTeacherId = teacher.ValueOrThrow.Id,
                ToTeacherName = teacher.ValueOrThrow.FullName,
                ActorUserId = CurrentUserId
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.StudentGroups.GetWithTeachersAsync(group.Id, cancellationToken);

        return Result.Success((created ?? group).ToDto(studentCount: 0));
    }

    /// <inheritdoc />
    public async Task<Result<StudentGroupDto>> UpdateAsync(
        int id,
        SaveStudentGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return Result.Failure<StudentGroupDto>(load.Error!, load.ErrorCode);
        }

        var group = load.ValueOrThrow;
        var name = request.Name?.Trim() ?? string.Empty;

        // Loại trừ chính nhóm đang sửa, nếu không đổi mỗi mô tả cũng tự báo trùng tên với chính mình.
        if (await _unitOfWork.StudentGroups.IsNameTakenAsync(name, group.Id, cancellationToken))
        {
            return Result.Conflict<StudentGroupDto>("Tên lớp đã tồn tại.");
        }

        var term = await ResolveTermAsync(request.TermId, cancellationToken);
        if (term.IsFailure)
        {
            return Result.Failure<StudentGroupDto>(term.Error!, term.ErrorCode);
        }

        group.Name = name;
        group.Description = NormalizeOptional(request.Description);
        group.AcademicTermId = term.ValueOrThrow?.Id;
        group.AcademicTerm = term.ValueOrThrow;
        group.UpdatedAt = DateTime.UtcNow;

        // Không có vòng lặp cập nhật student.group ở đây, và đó là điểm mấu chốt: học sinh chỉ giữ
        // GroupId, nên tên mới có hiệu lực ngay ở mọi màn hình mà không phải ghi lại bản ghi nào (§4.4).
        _unitOfWork.StudentGroups.Update(group);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var count = await _unitOfWork.Users.CountStudentsInGroupAsync(group.Id, cancellationToken);

        return Result.Success(group.ToDto(count));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        // Lớp khoá mà đã rỗng thì xoá được: không còn gì để bảo vệ.
        var load = await LoadWritableAsync(id, cancellationToken, allowLocked: true);
        if (load.IsFailure)
        {
            return load;
        }

        var group = load.ValueOrThrow;
        var members = await _unitOfWork.Users.GetStudentsByGroupAsync(group.Id, cancellationToken);

        // Khoá ngoại phía lớp đặt OnDelete(Cascade) nên xoá nhóm còn người sẽ *chạy được* và âm thầm gỡ cả
        // lớp khỏi danh sách lớp của từng em. Chặn ở đây để việc đó phải là một quyết định có ý thức: gỡ học sinh
        // khỏi lớp trước, rồi mới xoá.
        //
        // Đếm cả tài khoản chờ xoá, dù sĩ số không tính chúng. Để khoá ngoại gỡ các em thì khôi phục
        // xong em quay lại không có lớp mà không ai được hỏi — trong khi màn hình xoá lớp vừa nói
        // "không ai bị ảnh hưởng". Gỡ khỏi lớp là thao tác có sẵn, nên chặn không bắt ai chờ 60 ngày.
        if (members.Count > 0)
        {
            var retired = members.Count(m => m.Status is UserStatus.PendingDeletion or UserStatus.Deleted);
            var enrolled = members.Count - retired;

            return Result.Conflict((enrolled, retired) switch
            {
                (_, 0) => $"Không thể xoá nhóm còn {enrolled} học sinh. Hãy chuyển học sinh sang nhóm khác hoặc gỡ khỏi lớp trước.",
                (0, _) => $"Không thể xoá lớp còn {retired} tài khoản đang chờ xoá. Hãy gỡ các em khỏi lớp trước.",
                _ => $"Không thể xoá nhóm còn {enrolled} học sinh và {retired} tài khoản đang chờ xoá. "
                     + "Hãy chuyển hoặc gỡ học sinh khỏi lớp, và gỡ các tài khoản chờ xoá khỏi lớp trước."
            });
        }

        // Khoá ngoại từ bài giao về lớp là SetNull: xoá lớp rỗng mà còn bài đã làm hoặc đã hết hạn thì các bài đó lặng lẽ mất tên lớp và
        // kỳ học (rơi về "Chưa phân kỳ"), cùng lịch sử lớp đi theo. Lớp có lịch sử bài thì giữ lại; muốn dừng lớp thì khoá.
        if (await _unitOfWork.ExamAssignments.AnyViaGroupAsync(group.Id, cancellationToken))
        {
            return Result.Conflict(
                "Lớp đã có bài kiểm tra được giao (đã làm hoặc đã hết hạn) nên không xoá được: xoá sẽ làm các bài đó mất tên lớp và kỳ học. "
                + "Hãy khoá lớp thay vì xoá.");
        }

        _unitOfWork.StudentGroups.Remove(group);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<StudentDto>>> ListMembersAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var group = await LoadReadableAsync(id, cancellationToken);
        if (group is null)
        {
            return Result.NotFound<IReadOnlyList<StudentDto>>(NotFoundMessage);
        }

        var students = await _unitOfWork.Users.GetStudentsByGroupAsync(group.Id, cancellationToken);

        return Result.Success(await ToStudentDtosAsync(students, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<StudentDto>>> ListAssignableAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var group = await LoadReadableAsync(id, cancellationToken);
        if (group is null)
        {
            return Result.NotFound<IReadOnlyList<StudentDto>>(NotFoundMessage);
        }

        // Mọi em chưa học lớp này, kể cả em đang học lớp khác: một học sinh học được nhiều lớp cùng lúc nên thêm vào đây không
        // kéo em ra khỏi chỗ nào.
        var students = await _unitOfWork.Users.GetAssignableStudentsAsync(group.Id, cancellationToken);

        return Result.Success(await ToStudentDtosAsync(students, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<AddMembersResponse>> AddMembersAsync(
        int id,
        AddMembersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return Result.Failure<AddMembersResponse>(load.Error!, load.ErrorCode);
        }

        var group = load.ValueOrThrow;

        // Khử trùng lặp trước khi đếm, nếu không chọn nhầm một em hai lần sẽ báo "đã thêm 2".
        var requestedIds = request.StudentIds.Distinct().ToList();

        // Chỉ trả về id thật sự tồn tại và đúng vai trò Student — id của giáo viên lọt vào danh
        // sách cũng không biến họ thành thành viên lớp.
        var found = await _unitOfWork.Users.GetStudentsByIdsAsync(requestedIds, cancellationToken);

        var alreadyIn = await _unitOfWork.StudentGroups.GetMemberIdsAsync(
            group.Id,
            found.Select(student => student.Id),
            cancellationToken);

        var added = 0;
        var alreadyMember = 0;
        var retired = 0;
        var now = DateTime.UtcNow;

        foreach (var student in found)
        {
            // Tài khoản chờ xoá hoặc đã xoá không được đưa trở lại lớp. Một em đã rời trường mà vẫn
            // đếm vào sĩ số thì mọi con số của lớp sai theo — kể cả những hộp xác nhận "N học sinh
            // sẽ nhận bài" mà giáo viên dựa vào để quyết định.
            if (student.Status is UserStatus.PendingDeletion or UserStatus.Deleted)
            {
                retired++;
                continue;
            }

            if (alreadyIn.Contains(student.Id))
            {
                alreadyMember++;
                continue;
            }

            // Chỉ thêm một dòng thành viên: em học được nhiều lớp cùng lúc nên không đụng tới các lớp khác của em, và cũng không
            // ghi lên dòng tài khoản (không có gì để tranh nhau với một lần khoá hay đổi mật khẩu đồng thời).
            _unitOfWork.StudentGroups.AddMember(new StudentGroupMember
            {
                StudentGroupId = group.Id,
                StudentId = student.Id,
                JoinedAt = now
            });

            await _unitOfWork.AccountChanges.AddAsync(
                NewChange(student.Id, AccountChangeKind.AddedToGroup, reason: null, from: null, to: group),
                cancellationToken);
            added++;
        }

        // Một lần ghi cho cả mẻ: mạng đứt giữa chừng thì không em nào vào nhóm, thay vì mười em vào và mười em không. Hai
        // lần xếp lớp đồng thời cho cùng một em va nhau ở khoá chính (lớp, học sinh) và người đến sau nhận 409.
        if (added > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var count = await _unitOfWork.Users.CountStudentsInGroupAsync(group.Id, cancellationToken);

        return Result.Success(new AddMembersResponse
        {
            Added = added,
            SkippedAlreadyMember = alreadyMember,
            SkippedNotFound = requestedIds.Count - found.Count,
            SkippedRetired = retired,
            StudentCount = count
        });
    }

    /// <inheritdoc />
    public async Task<Result<MemberChangeResponse>> RemoveMemberAsync(
        int id,
        int studentId,
        RemoveMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return Result.Failure<MemberChangeResponse>(load.Error!, load.ErrorCode);
        }

        var group = load.ValueOrThrow;

        // Khoá dòng tài khoản rồi tới các dòng giao bài của em (xem IExamAssignmentRepository.LockByStudentAsync): một
        // em đang bấm "Vào thi" không bị gỡ bài ngay dưới tay, và hai quản trị viên cùng gỡ một em phải xếp hàng.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var student = await _unitOfWork.Users.LockForUpdateAsync(studentId, cancellationToken);
        var membership = student is { Role: UserRole.Student }
            ? await _unitOfWork.StudentGroups.GetMemberAsync(group.Id, studentId, cancellationToken)
            : null;

        // Học sinh không tồn tại và học sinh không học lớp này cho cùng một câu trả lời: dưới góc nhìn
        // của URL này, cả hai đều là "không có thành viên đó trong nhóm này".
        if (student is null || membership is null)
        {
            return Result.NotFound<MemberChangeResponse>(StudentNotFoundMessage);
        }

        var assignments = await _unitOfWork.ExamAssignments.LockByStudentAsync(student.Id, cancellationToken);
        var outcome = RevokeOpenAssignments(assignments, group.Id, DateTime.UtcNow);

        // Gỡ khỏi lớp là xoá dòng thành viên, không đụng tới tài khoản (§4.4) và không đụng tới các lớp khác của em. Em đã làm
        // bài vẫn được gỡ (nghỉ học, chuyển trường); bài đã làm và điểm giữ nguyên.
        _unitOfWork.StudentGroups.RemoveMember(membership);

        await _unitOfWork.AccountChanges.AddAsync(
            NewChange(student.Id, AccountChangeKind.RemovedFromGroup, request.Reason.Trim(), from: group, to: null),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(outcome);
    }

    /// <inheritdoc />
    public async Task<Result<MemberChangeResponse>> TransferMemberAsync(
        int id,
        int studentId,
        TransferMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return Result.Failure<MemberChangeResponse>(load.Error!, load.ErrorCode);
        }

        var group = load.ValueOrThrow;

        if (request.TargetGroupId == group.Id)
        {
            return Result.Failure<MemberChangeResponse>("Lớp đích phải khác lớp hiện tại.");
        }

        var target = await _unitOfWork.StudentGroups.GetByIdAsync(request.TargetGroupId, cancellationToken);
        if (target is null)
        {
            return Result.NotFound<MemberChangeResponse>(TargetNotFoundMessage);
        }

        if (target.IsLocked)
        {
            return Result.Conflict<MemberChangeResponse>(LockedTargetMessage(target));
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var student = await _unitOfWork.Users.LockForUpdateAsync(studentId, cancellationToken);
        var membership = student is { Role: UserRole.Student }
            ? await _unitOfWork.StudentGroups.GetMemberAsync(group.Id, studentId, cancellationToken)
            : null;

        if (student is null || membership is null)
        {
            return Result.NotFound<MemberChangeResponse>(StudentNotFoundMessage);
        }

        // Cùng lý do với AddMembersAsync: một em đã rời trường không được đưa trở lại một lớp.
        if (student.Status is UserStatus.PendingDeletion or UserStatus.Deleted)
        {
            return Result.Conflict<MemberChangeResponse>(
                "Tài khoản này đang chờ xoá hoặc đã xoá nên không chuyển lớp được. Hãy khôi phục tài khoản trước.");
        }

        var alreadyInTarget = await _unitOfWork.StudentGroups.GetMemberIdsAsync(target.Id, [student.Id], cancellationToken);
        if (alreadyInTarget.Contains(student.Id))
        {
            return Result.Conflict<MemberChangeResponse>(
                $"Em đã học lớp {target.Name} rồi. Muốn rời lớp này thì dùng Gỡ khỏi lớp.");
        }

        // Chuyển = gỡ khỏi lớp này rồi xếp vào lớp kia trong một bước. Khoá "đã làm bài thì không chuyển" của mô hình một-lớp
        // không còn nghĩa: em học được nhiều lớp cùng lúc nên luôn có thể được xếp thêm vào lớp kia, và gỡ khỏi lớp này vốn đã
        // luôn làm được.
        var assignments = await _unitOfWork.ExamAssignments.LockByStudentAsync(student.Id, cancellationToken);
        var now = DateTime.UtcNow;
        var outcome = RevokeOpenAssignments(assignments, group.Id, now);

        _unitOfWork.StudentGroups.RemoveMember(membership);
        _unitOfWork.StudentGroups.AddMember(new StudentGroupMember
        {
            StudentGroupId = target.Id,
            StudentId = student.Id,
            JoinedAt = now
        });

        await _unitOfWork.AccountChanges.AddAsync(
            NewChange(student.Id, AccountChangeKind.MovedGroup, request.Reason.Trim(), from: group, to: target),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(outcome);
    }

    /// <inheritdoc />
    public async Task<Result<StudentGroupDto>> PublishAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return Result.Failure<StudentGroupDto>(load.Error!, load.ErrorCode);
        }

        var group = load.ValueOrThrow;

        if (group.IsPublished)
        {
            return Result.Conflict<StudentGroupDto>("Lớp này đã được công bố rồi.");
        }

        // Giáo viên là người nhìn thấy lớp sau khi công bố; lớp chưa có ai phụ trách (lớp cũ từ trước khi có quy tắc
        // "mỗi lớp một giáo viên") thì công bố chẳng tới ai. Học sinh thì vẫn được phép chưa có: lớp mới lập đầu năm.
        if (group.Teachers.Count == 0)
        {
            return Result.Conflict<StudentGroupDto>(
                "Lớp chưa có giáo viên phụ trách nên chưa công bố được. Hãy chọn giáo viên ở phần Thiết lập.");
        }

        // Cố ý KHÔNG bắt buộc phải có học sinh hoặc giáo viên trước khi công bố. Một lớp mới lập
        // đầu năm, đã có tên và đã phân công giáo viên nhưng chưa xếp em nào, vẫn là một lớp có
        // thật. Chặn ở đây là bắt quản trị viên làm mọi việc theo đúng một thứ tự. Thay vào đó
        // giao diện cảnh báo khi lớp rỗng hoặc chưa có ai dạy — nhắc chứ không cấm.
        group.IsPublished = true;
        group.PublishedAt = DateTime.UtcNow;
        group.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.StudentGroups.Update(group);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var withTeachers = await _unitOfWork.StudentGroups.GetWithTeachersAsync(group.Id, cancellationToken);
        var count = await _unitOfWork.Users.CountStudentsInGroupAsync(group.Id, cancellationToken);

        return Result.Success((withTeachers ?? group).ToDto(count));
    }

    /// <inheritdoc />
    public async Task<Result<StudentGroupDto>> UnpublishAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return Result.Failure<StudentGroupDto>(load.Error!, load.ErrorCode);
        }

        var group = load.ValueOrThrow;

        if (!group.IsPublished)
        {
            return Result.Conflict<StudentGroupDto>("Lớp này đang là bản nháp.");
        }

        // Giữ nguyên PublishedAt: nó ghi lại lần công bố đầu tiên, và xoá đi là mất một mốc thời
        // gian có thật chỉ vì lớp tạm ẩn đi vài phút.
        group.IsPublished = false;
        group.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.StudentGroups.Update(group);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var withTeachers = await _unitOfWork.StudentGroups.GetWithTeachersAsync(group.Id, cancellationToken);
        var count = await _unitOfWork.Users.CountStudentsInGroupAsync(group.Id, cancellationToken);

        return Result.Success((withTeachers ?? group).ToDto(count));
    }

    /// <inheritdoc />
    public async Task<Result<StudentGroupDto>> LockAsync(
        int id,
        LockGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var load = await LoadWritableAsync(id, cancellationToken, allowLocked: true);
        if (load.IsFailure)
        {
            return Result.Failure<StudentGroupDto>(load.Error!, load.ErrorCode);
        }

        var group = load.ValueOrThrow;

        if (group.IsLocked)
        {
            return Result.Conflict<StudentGroupDto>("Lớp này đã khoá rồi.");
        }

        var now = DateTime.UtcNow;
        group.IsLocked = true;
        group.LockedAt = now;
        group.UpdatedAt = now;

        _unitOfWork.StudentGroups.Update(group);

        // Khoá lớp không động tới thành viên, bài đã giao hay bảng điểm; thứ duy nhất đổi là lớp thôi nhận thêm người và
        // bài mới. Nên lý do nằm ở lịch sử của lớp chứ không phải của từng học sinh.
        await _unitOfWork.GroupChanges.AddAsync(
            new GroupChange
            {
                GroupId = group.Id,
                Kind = GroupChangeKind.Locked,
                Reason = request.Reason.Trim(),
                ActorUserId = CurrentUserId
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var count = await _unitOfWork.Users.CountStudentsInGroupAsync(group.Id, cancellationToken);

        return Result.Success(group.ToDto(count));
    }

    /// <inheritdoc />
    public async Task<Result<StudentGroupDto>> UnlockAsync(int id, CancellationToken cancellationToken = default)
    {
        var load = await LoadWritableAsync(id, cancellationToken, allowLocked: true);
        if (load.IsFailure)
        {
            return Result.Failure<StudentGroupDto>(load.Error!, load.ErrorCode);
        }

        var group = load.ValueOrThrow;

        if (!group.IsLocked)
        {
            return Result.Conflict<StudentGroupDto>("Lớp này đang không bị khoá.");
        }

        // Giữ LockedAt? Không: nó chỉ có nghĩa khi lớp đang khoá; các lần khoá và mở khoá cũ nằm ở lịch sử lớp.
        group.IsLocked = false;
        group.LockedAt = null;
        group.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.StudentGroups.Update(group);

        await _unitOfWork.GroupChanges.AddAsync(
            new GroupChange
            {
                GroupId = group.Id,
                Kind = GroupChangeKind.Unlocked,
                ActorUserId = CurrentUserId
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var count = await _unitOfWork.Users.CountStudentsInGroupAsync(group.Id, cancellationToken);

        return Result.Success(group.ToDto(count));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<GroupTeacherDto>>> ListTeachersAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<IReadOnlyList<GroupTeacherDto>>(AdminOnlyMessage);
        }

        // Cả tài khoản đang bị khoá, kèm trạng thái: quản trị viên hiểu vì sao một người không chọn được thay vì đi tìm
        // một cái tên đã biến mất khỏi danh sách.
        var teachers = await _unitOfWork.Users.GetTeachersAsync(cancellationToken);

        return Result.Success<IReadOnlyList<GroupTeacherDto>>(
            teachers.Select(teacher => teacher.ToGroupTeacherDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<StudentGroupDto>> ChangeTeacherAsync(
        int id,
        ChangeTeacherRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<StudentGroupDto>(AdminOnlyMessage);
        }

        var teacher = await ResolveTeacherAsync(request.TeacherId, cancellationToken);
        if (teacher.IsFailure)
        {
            return Result.Failure<StudentGroupDto>(teacher.Error!, teacher.ErrorCode);
        }

        // Một lớp có đúng một giáo viên: đổi người là thay cả tập, và hai quản trị viên cùng đổi một lúc phải xếp hàng,
        // nếu không mỗi người thêm giáo viên của mình rồi cùng gỡ người cũ sẽ để lại hai giáo viên.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        if (!await _unitOfWork.StudentGroups.LockForUpdateAsync(id, cancellationToken))
        {
            return Result.NotFound<StudentGroupDto>(NotFoundMessage);
        }

        var group = await _unitOfWork.StudentGroups.GetWithTeachersAsync(id, cancellationToken);
        if (group is null)
        {
            return Result.NotFound<StudentGroupDto>(NotFoundMessage);
        }

        if (group.IsLocked)
        {
            return Result.Conflict<StudentGroupDto>(LockedGroupMessage);
        }

        var target = teacher.ValueOrThrow;
        var alreadyThere = group.Teachers.Any(assignment => assignment.TeacherId == target.Id);
        var others = group.Teachers.Where(assignment => assignment.TeacherId != target.Id).ToList();

        // Đổi người giữa chừng thì phải nói vì sao: lý do được lưu vào lịch sử của lớp. Lớp chưa có ai phụ trách (lớp cũ)
        // thì không có gì để "đổi" nên không cần; chọn lại đúng người đang phụ trách cũng vậy.
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (others.Count > 0 && reason is null)
        {
            return Result.Failure<StudentGroupDto>("Vui lòng nhập lý do đổi giáo viên.");
        }

        var previous = others.FirstOrDefault();
        var previousName = previous is null
            ? null
            : (await _unitOfWork.Users.GetByIdAsync(previous.TeacherId, cancellationToken))?.FullName;

        // Lớp cũ có nhiều giáo viên (dữ liệu từ thời còn phân công nhiều người) thì lần đổi này dọn luôn về một.
        foreach (var other in others)
        {
            _unitOfWork.StudentGroups.RemoveTeacher(other);
        }

        if (!alreadyThere)
        {
            _unitOfWork.StudentGroups.AddTeacher(new StudentGroupTeacher
            {
                StudentGroupId = id,
                TeacherId = target.Id,
                AssignedAt = DateTime.UtcNow
            });
        }

        if (others.Count > 0 || !alreadyThere)
        {
            // Bài đã giao gắn với học sinh chứ không gắn với giáo viên, nên đổi người phụ trách không đụng tới chúng.
            group.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.StudentGroups.Update(group);

            await _unitOfWork.GroupChanges.AddAsync(
                new GroupChange
                {
                    GroupId = id,
                    Kind = GroupChangeKind.TeacherChanged,
                    Reason = reason,
                    FromTeacherId = previous?.TeacherId,
                    FromTeacherName = previousName,
                    ToTeacherId = target.Id,
                    ToTeacherName = target.FullName,
                    ActorUserId = CurrentUserId
                },
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        // Nạp lại để DTO mang đúng giáo viên mới (navigation của dòng vừa thêm chưa được nạp).
        var reloaded = await _unitOfWork.StudentGroups.GetWithTeachersAsync(id, cancellationToken);
        var count = await _unitOfWork.Users.CountStudentsInGroupAsync(id, cancellationToken);

        return Result.Success((reloaded ?? group).ToDto(count));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<GroupChangeDto>>> ListChangesAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<IReadOnlyList<GroupChangeDto>>(AdminOnlyMessage);
        }

        if (await _unitOfWork.StudentGroups.GetByIdAsync(id, cancellationToken) is null)
        {
            return Result.NotFound<IReadOnlyList<GroupChangeDto>>(NotFoundMessage);
        }

        var rows = await _unitOfWork.GroupChanges.GetByGroupAsync(id, cancellationToken);

        return Result.Success<IReadOnlyList<GroupChangeDto>>(rows.Select(change => change.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<GroupExamDto>>> ListExamsAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        // Quản trị viên đọc mọi lớp; giáo viên chỉ đọc lớp đã công bố mình dạy (lớp khác 404, như mọi nơi). Giáo viên thấy cả đề do
        // giáo viên trước của lớp soạn: người phụ trách lớp cần biết lớp đã được giao những bài nào.
        if (await LoadReadableAsync(id, cancellationToken) is null)
        {
            return Result.NotFound<IReadOnlyList<GroupExamDto>>(NotFoundMessage);
        }

        var assignments = await _unitOfWork.ExamAssignments.GetByGroupAsync(id, cancellationToken);
        var now = DateTime.UtcNow;

        // Mỗi đề một dòng. Đề nào đã giao cho lớp này thì có mặt, kể cả khi em nhận bài đã rời lớp hoặc đề đã đóng từ lâu:
        // đây là lịch sử học của lớp, không phải bảng theo dõi của một đề.
        IReadOnlyList<GroupExamDto> exams = assignments
            .GroupBy(assignment => assignment.ExamId)
            .Select(rows => rows.ToGroupExamDto(now))
            .OrderByDescending(exam => exam.CloseAt)
            .ThenByDescending(exam => exam.ExamId)
            .ToList();

        return Result.Success(exams);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ExamAssignmentDto>>> ListExamResultsAsync(
        int id,
        int examId,
        CancellationToken cancellationToken = default)
    {
        if (await LoadReadableAsync(id, cancellationToken) is null)
        {
            return Result.NotFound<IReadOnlyList<ExamAssignmentDto>>(NotFoundMessage);
        }

        var assignments = await _unitOfWork.ExamAssignments.GetByExamAsync(examId, cancellationToken);
        var now = DateTime.UtcNow;

        // Chỉ những em được giao QUA LỚP NÀY: đề có thể đã giao cho nhiều lớp, và người xem được phép đọc lớp này chứ không
        // phải cả đề. Lọc ở server để một giáo viên không dựa vào id đề mà đọc điểm của lớp khác.
        IReadOnlyList<ExamAssignmentDto> rows = assignments
            .Where(assignment => assignment.AssignedViaGroupId == id)
            .Select(assignment => assignment.ToDto(now))
            .ToList();

        return Result.Success(rows);
    }

    /// <summary>
    /// Giáo viên phụ trách phải là một tài khoản giáo viên có thật và đang hoạt động. Trả 400 khi id lạ (đó là lỗi của dữ
    /// liệu gửi lên) và 409 khi tài khoản đang bị khoá (lỗi của trạng thái, mở khoá là hết).
    /// </summary>
    /// <summary>Kỳ học gửi lên (không bắt buộc): bỏ trống là chưa phân kỳ; id không tồn tại thì báo lỗi.</summary>
    private async Task<Result<AcademicTerm?>> ResolveTermAsync(int? termId, CancellationToken cancellationToken)
    {
        if (termId is null)
        {
            return Result.Success<AcademicTerm?>(null);
        }

        var term = await _unitOfWork.AcademicTerms.GetByIdAsync(termId.Value, cancellationToken);

        return term is null
            ? Result.Failure<AcademicTerm?>("Kỳ học không tồn tại.")
            : Result.Success<AcademicTerm?>(term);
    }

    private async Task<Result<User>> ResolveTeacherAsync(int teacherId, CancellationToken cancellationToken)
    {
        var found = await _unitOfWork.Users.GetTeachersByIdsAsync([teacherId], cancellationToken);
        var teacher = found.FirstOrDefault();

        if (teacher is null)
        {
            return Result.Failure<User>("Không tìm thấy giáo viên này.");
        }

        // Giao lớp cho một người không đăng nhập nổi là tạo ra một lớp trông như đã có người phụ trách trong khi thực tế
        // thì không.
        if (teacher.Status != UserStatus.Active)
        {
            return Result.Conflict<User>(
                "Tài khoản giáo viên này đang bị khoá nên chưa phân công được. Hãy mở khoá hoặc chọn giáo viên khác.");
        }

        return Result.Success(teacher);
    }

    private const string AdminOnlyMessage =
        "Chỉ quản trị viên mới lập và sắp xếp lớp. Giáo viên được phân công vào lớp, không tự tạo.";

    private int CurrentUserId => _currentUser.RequireUserId();

    /// <summary>
    /// Nạp lớp cho thao tác <b>đọc</b>. Trả NULL khi lớp không tồn tại <i>hoặc</i> nằm ngoài phạm
    /// vi người gọi — chỗ gọi biến cả hai thành 404 (§4.2).
    /// </summary>
    /// <remarks>
    /// Với giáo viên, "trong phạm vi" gồm <b>hai</b> điều kiện: được phân công dạy lớp đó, và lớp
    /// đã công bố. Một lớp quản trị viên đang dựng dở không được rò sang màn hình của ai khác, kể
    /// cả người sẽ dạy nó.
    /// </remarks>
    private async Task<StudentGroup?> LoadReadableAsync(int id, CancellationToken cancellationToken)
    {
        var group = await _unitOfWork.StudentGroups.GetWithTeachersAsync(id, cancellationToken);
        if (group is null)
        {
            return null;
        }

        if (_currentUser.IsInRole(UserRole.Admin))
        {
            return group;
        }

        return await _unitOfWork.StudentGroups.IsTeachingAsync(id, CurrentUserId, cancellationToken)
            ? group
            : null;
    }

    /// <summary>
    /// Nạp lớp cho thao tác <b>ghi</b>. Chỉ quản trị viên qua được.
    /// </summary>
    /// <remarks>
    /// Đảo ngược hẳn so với mô hình cũ, nơi giáo viên sở hữu lớp còn quản trị viên chỉ được đọc: lớp là
    /// đơn vị hành chính của cả tổ chức, nên chỉ người đứng đầu tổ chức dựng và sắp xếp lớp.
    ///
    /// Lớp đã khoá thì chỉ đọc: mọi thao tác ghi bị từ chối (409) trừ khi gọi với <c>allowLocked</c> (khoá,
    /// mở khoá, xoá lớp rỗng).
    ///
    /// Tầng Api đã chặn bằng <c>[Authorize(Roles = Admin)]</c>; lớp kiểm ở đây để service đứng
    /// vững cả khi được gọi từ chỗ khác.
    /// </remarks>
    private async Task<Result<StudentGroup>> LoadWritableAsync(
        int id,
        CancellationToken cancellationToken,
        bool allowLocked = false)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<StudentGroup>(AdminOnlyMessage);
        }

        var group = await _unitOfWork.StudentGroups.GetWithTeachersAsync(id, cancellationToken);
        if (group is null)
        {
            return Result.NotFound<StudentGroup>(NotFoundMessage);
        }

        return group.IsLocked && !allowLocked
            ? Result.Conflict<StudentGroup>(LockedGroupMessage)
            : Result.Success(group);
    }

    private bool TryGetTeacherId(out int teacherId)
    {
        teacherId = _currentUser.UserId ?? 0;

        return _currentUser.IsAuthenticated
               && _currentUser.UserId.HasValue
               && _currentUser.IsInRole(UserRole.Teacher);
    }

    /// <summary>Gắn sĩ số cho cả danh sách bằng một truy vấn <c>GROUP BY</c>, không phải N+1.</summary>
    private async Task<IReadOnlyList<StudentGroupDto>> AttachCountsAsync(
        IReadOnlyList<StudentGroup> groups,
        CancellationToken cancellationToken)
    {
        if (groups.Count == 0)
        {
            return [];
        }

        var counts = await _unitOfWork.Users.CountStudentsByGroupAsync(
            groups.Select(group => group.Id),
            cancellationToken);

        return groups
            .Select(group => group.ToDto(counts.TryGetValue(group.Id, out var count) ? count : 0))
            .ToList();
    }

    /// <summary>
    /// Đổi danh sách học sinh sang DTO, lấy thống kê của cả danh sách trong một truy vấn. Giáo viên chỉ nhận những lớp mình dạy
    /// trong danh sách lớp của từng em.
    /// </summary>
    private async Task<IReadOnlyList<StudentDto>> ToStudentDtosAsync(
        IReadOnlyList<User> students,
        CancellationToken cancellationToken)
    {
        if (students.Count == 0)
        {
            return [];
        }

        IReadOnlySet<int>? visibleGroupIds = null;
        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            visibleGroupIds = (await _unitOfWork.StudentGroups.GetForTeacherAsync(CurrentUserId, search: null, cancellationToken))
                .Select(group => group.Id)
                .ToHashSet();
        }

        var stats = await _unitOfWork.ExamAttempts.GetStatsByStudentsAsync(
            students.Select(student => student.Id),
            visibleGroupIds is null ? null : new StudentStatsScope(visibleGroupIds, CurrentUserId),
            cancellationToken);

        return students
            .Select(student =>
            {
                var (completed, averageScore, started, assigned) = stats.TryGetValue(student.Id, out var row) ? row : (0, 0m, 0, 0);

                return student.ToStudentDto(completed, averageScore, started, visibleGroupIds: visibleGroupIds, assignedExams: assigned);
            })
            .ToList();
    }

    /// <summary>
    /// Dựng một dòng lịch sử của học sinh. Tên lớp được chép lại để dòng này đọc đúng như lúc xảy ra, dù lớp sau đó
    /// đổi tên hay bị xoá.
    /// </summary>
    private AccountChange NewChange(
        int studentId,
        AccountChangeKind kind,
        string? reason,
        StudentGroup? from,
        StudentGroup? to) =>
        new()
        {
            AccountId = studentId,
            Kind = kind,
            Reason = reason,
            FromGroupId = from?.Id,
            FromGroupName = from?.Name,
            ToGroupId = to?.Id,
            ToGroupName = to?.Name,
            ActorUserId = _currentUser.UserId
        };

    /// <summary>
    /// Thu hồi khỏi em những bài <b>lớp cũ</b> giao mà em chưa bắt đầu và chưa hết hạn; mọi bài khác được giữ.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bài đã bắt đầu giữ nguyên vì thu hồi là xoá luôn bài em đã làm (cùng nguyên tắc với <c>RevokeClassAsync</c>).
    /// Bài đã hết hạn mà em không làm cũng giữ: đó là bằng chứng em đã vắng mặt, và người quản lý cần còn nhìn thấy nó.
    /// </para>
    /// <para>
    /// Chỉ đụng tới bài giao <b>qua lớp này</b> (<c>AssignedViaGroupId</c>); lượt giao lẻ cũ không gắn với lớp nào thì
    /// không phải việc của lần rời lớp này. Phải gọi sau <c>LockByStudentAsync</c>, trong cùng transaction.
    /// </para>
    /// </remarks>
    private MemberChangeResponse RevokeOpenAssignments(
        IReadOnlyList<ExamAssignment> assignments,
        int fromGroupId,
        DateTime now)
    {
        var viaGroup = assignments.Where(assignment => assignment.AssignedViaGroupId == fromGroupId).ToList();
        var revocable = viaGroup
            .Where(assignment => assignment.Attempt is null
                                 && assignment.ResolveStatus(now) == AssignmentStatus.Assigned)
            .ToList();

        _unitOfWork.ExamAssignments.RemoveRange(revocable);

        return new MemberChangeResponse
        {
            RevokedExams = revocable.Count,
            KeptExams = viaGroup.Count - revocable.Count
        };
    }

    /// <summary>Chuỗi rỗng nghĩa là xoá trắng mô tả, nên quy về NULL trước khi lưu.</summary>
    private static string? NormalizeOptional(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
