using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.StudentGroups;
using EduQuiz.Application.DTOs.Students;
using EduQuiz.Application.Interfaces.Security;
using EduQuiz.Application.Interfaces.Services;
using EduQuiz.Application.Interfaces.UnitOfWork;
using EduQuiz.Application.Mappings;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Services;

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
/// <item><b>Đổi tên nhóm không lan xuống bản ghi học sinh.</b> Học sinh chỉ giữ khoá ngoại
/// <c>GroupId</c>; tên đọc qua JOIN. Vòng lặp <c>syncGroupCounts()</c> và đoạn cập nhật
/// <c>student.group</c> của frontend biến mất hoàn toàn ở đây.</item>
/// <item><b>Xoá nhóm không xoá tài khoản.</b> Chỉ nhóm rỗng mới xoá được, nên không có tình huống
/// "xoá lớp mất luôn học sinh" ngay từ đầu.</item>
/// </list>
/// </remarks>
public sealed class StudentGroupService : IStudentGroupService
{
    private const string NotFoundMessage = "Không tìm thấy nhóm lớp.";
    private const string StudentNotFoundMessage = "Không tìm thấy học sinh.";

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
        if (normalizedStatus is not ("all" or "draft" or "published"))
        {
            return Result.Failure<IReadOnlyList<StudentGroupDto>>(
                "Bộ lọc trạng thái chỉ nhận 'draft', 'published' hoặc 'all'.");
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
        SaveStudentGroupRequest request,
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

        var group = new StudentGroup
        {
            Name = name,
            Description = NormalizeOptional(request.Description),
            CreatedByUserId = CurrentUserId,

            // Lớp mới luôn là bản nháp. Quản trị viên còn phải xếp học sinh và phân công giáo viên
            // trước khi nó có nghĩa gì; hiện ngay lúc vừa tạo thì giáo viên thấy một lớp rỗng và
            // giao bài cho nó cũng chẳng tới ai.
            IsPublished = false
        };

        await _unitOfWork.StudentGroups.AddAsync(group, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(group.ToDto(studentCount: 0));
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

        group.Name = name;
        group.Description = NormalizeOptional(request.Description);
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
        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return load;
        }

        var group = load.ValueOrThrow;
        var count = await _unitOfWork.Users.CountStudentsInGroupAsync(group.Id, cancellationToken);

        // Khoá ngoại đặt OnDelete(SetNull) nên xoá nhóm còn người sẽ *chạy được* và âm thầm đẩy cả
        // lớp về "chưa phân nhóm". Chặn ở đây để việc đó phải là một quyết định có ý thức: giáo viên
        // chuyển học sinh đi trước, rồi mới xoá.
        if (count > 0)
        {
            return Result.Conflict(
                $"Không thể xoá nhóm còn {count} học sinh. Hãy chuyển học sinh sang nhóm khác trước.");
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

        // Truy vấn trên không nạp navigation Group, nên truyền thẳng tên nhóm đang có trong tay.
        return Result.Success(await ToStudentDtosAsync(students, group.Name, cancellationToken));
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

        // Gồm cả học sinh đang thuộc nhóm khác, đúng như frontend đang làm: thêm vào đây nghĩa là
        // *chuyển* nhóm. Mỗi học sinh chỉ thuộc một nhóm, nên loại họ ra sẽ khiến việc chuyển lớp
        // phải đi vòng qua màn hình sửa hồ sơ. Tên nhóm hiện tại có sẵn trong DTO để giáo viên thấy
        // rõ em nào đang ở đâu trước khi kéo sang.
        var students = await _unitOfWork.Users.GetAssignableStudentsAsync(group.Id, cancellationToken);

        return Result.Success(await ToStudentDtosAsync(students, groupName: null, cancellationToken));
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

        var added = 0;
        var alreadyMember = 0;
        var retired = 0;

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

            if (student.GroupId == group.Id)
            {
                alreadyMember++;
                continue;
            }

            student.GroupId = group.Id;
            student.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Users.Update(student);
            added++;
        }

        // Một lần ghi cho cả mẻ: mạng đứt giữa chừng thì không em nào vào nhóm, thay vì mười em vào
        // và mười em không.
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
    public async Task<Result> RemoveMemberAsync(
        int id,
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return load;
        }

        var group = load.ValueOrThrow;
        var student = await _unitOfWork.Users.GetByIdAsync(studentId, cancellationToken);

        // Học sinh không tồn tại và học sinh thuộc nhóm khác cho cùng một câu trả lời: dưới góc nhìn
        // của URL này, cả hai đều là "không có thành viên đó trong nhóm này".
        if (student is not { Role: UserRole.Student } || student.GroupId != group.Id)
        {
            return Result.NotFound(StudentNotFoundMessage);
        }

        // Gỡ khỏi nhóm là đặt GroupId về NULL, không đụng tới tài khoản (§4.4).
        student.GroupId = null;
        student.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(student);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
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
    public async Task<Result<IReadOnlyList<GroupTeacherDto>>> ListAssignableTeachersAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GroupTeacherDto>>(load.Error!, load.ErrorCode);
        }

        var teachers = await _unitOfWork.Users.GetAssignableTeachersAsync(id, cancellationToken);

        return Result.Success<IReadOnlyList<GroupTeacherDto>>(
            teachers.Select(teacher => teacher.ToGroupTeacherDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<AssignTeachersResponse>> AssignTeachersAsync(
        int id,
        AssignTeachersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return Result.Failure<AssignTeachersResponse>(load.Error!, load.ErrorCode);
        }

        var requestedIds = new HashSet<int>(request.TeacherIds ?? []);
        if (requestedIds.Count == 0)
        {
            return Result.Failure<AssignTeachersResponse>("Vui lòng chọn ít nhất một giáo viên.");
        }

        // Chỉ trả về id có thật và đúng vai trò Teacher. Chênh lệch số lượng chính là số id lạ —
        // không cần một vòng lặp riêng để đếm chúng.
        var teachers = await _unitOfWork.Users.GetTeachersByIdsAsync(requestedIds, cancellationToken);
        var notTeacher = requestedIds.Count - teachers.Count;

        var group = await _unitOfWork.StudentGroups.GetWithTeachersAsync(id, cancellationToken);
        var alreadyAssigned = group is null
            ? new HashSet<int>()
            : group.Teachers.Select(assignment => assignment.TeacherId).ToHashSet();

        var now = DateTime.UtcNow;
        var assigned = 0;
        var skippedAlready = 0;
        var skippedLocked = 0;

        foreach (var teacher in teachers)
        {
            if (alreadyAssigned.Contains(teacher.Id))
            {
                skippedAlready++;
                continue;
            }

            // Tài khoản bị khoá không phân công được: giao lớp cho một người không đăng nhập nổi
            // là tạo ra một lớp trông như đã có người phụ trách trong khi thực tế thì không.
            if (teacher.Status == UserStatus.Locked)
            {
                skippedLocked++;
                continue;
            }

            _unitOfWork.StudentGroups.AddTeacher(new StudentGroupTeacher
            {
                StudentGroupId = id,
                TeacherId = teacher.Id,
                AssignedAt = now
            });

            assigned++;
        }

        if (assigned > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new AssignTeachersResponse
        {
            Assigned = assigned,
            SkippedAlreadyAssigned = skippedAlready,
            SkippedNotTeacher = notTeacher,
            SkippedLocked = skippedLocked,
            TeacherCount = alreadyAssigned.Count + assigned
        });
    }

    /// <inheritdoc />
    public async Task<Result> UnassignTeacherAsync(
        int id,
        int teacherId,
        CancellationToken cancellationToken = default)
    {
        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return Result.Failure(load.Error!, load.ErrorCode);
        }

        var assignment = await _unitOfWork.StudentGroups.FindAssignmentAsync(id, teacherId, cancellationToken);
        if (assignment is null)
        {
            return Result.NotFound("Giáo viên này không phụ trách lớp đó.");
        }

        // Gỡ được cả khi lớp đã công bố và đang có bài kiểm tra chạy dở. Bài đã giao gắn với học
        // sinh chứ không gắn với giáo viên, nên chúng vẫn nguyên vẹn; còn chặn việc gỡ sẽ khiến
        // một giáo viên nghỉ việc mãi mãi nằm trong danh sách phụ trách.
        _unitOfWork.StudentGroups.RemoveTeacher(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
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
        var group = await _unitOfWork.StudentGroups.GetByIdAsync(id, cancellationToken);
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
    /// Đảo ngược hẳn so với mô hình cũ, nơi giáo viên sở hữu lớp còn quản trị viên chỉ được đọc.
    /// Lý do nằm ở lược đồ: <c>User.GroupId</c> là khoá ngoại đơn, nên một học sinh thuộc đúng một
    /// lớp. Để giáo viên sở hữu lớp thì hai người cùng dạy một em sẽ giành nhau em đó — người thêm
    /// sau kéo em ra khỏi lớp của người trước và làm hỏng mọi bài người trước đã giao.
    ///
    /// Tầng Api đã chặn bằng <c>[Authorize(Roles = Admin)]</c>; lớp kiểm ở đây để service đứng
    /// vững cả khi được gọi từ chỗ khác.
    /// </remarks>
    private async Task<Result<StudentGroup>> LoadWritableAsync(int id, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<StudentGroup>(AdminOnlyMessage);
        }

        var group = await _unitOfWork.StudentGroups.GetByIdAsync(id, cancellationToken);

        return group is not null
            ? Result.Success(group)
            : Result.NotFound<StudentGroup>(NotFoundMessage);
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

    /// <summary>Đổi danh sách học sinh sang DTO, lấy thống kê của cả danh sách trong một truy vấn.</summary>
    private async Task<IReadOnlyList<StudentDto>> ToStudentDtosAsync(
        IReadOnlyList<User> students,
        string? groupName,
        CancellationToken cancellationToken)
    {
        if (students.Count == 0)
        {
            return [];
        }

        var stats = await _unitOfWork.ExamAttempts.GetStatsByStudentsAsync(
            students.Select(student => student.Id),
            cancellationToken);

        return students
            .Select(student =>
            {
                var (completed, averageScore) = stats.TryGetValue(student.Id, out var row) ? row : (0, 0m);

                return student.ToStudentDto(completed, averageScore, groupName);
            })
            .ToList();
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
