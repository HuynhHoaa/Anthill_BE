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
        CancellationToken cancellationToken = default)
    {
        // NULL nghĩa là "mọi nhóm" — chỉ quản trị viên nhận giá trị đó.
        var groups = await _unitOfWork.StudentGroups.GetOwnedAsync(
            OwnerScope(),
            search,
            cancellationToken);

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

        if (!TryGetTeacherId(out var teacherId))
        {
            return Result.Forbidden<StudentGroupDto>(TeacherOnlyMessage);
        }

        var name = request.Name?.Trim() ?? string.Empty;

        // Phạm vi so trùng là trong nhóm của cùng một giáo viên: hai giáo viên đều có "Lớp 8A" là
        // chuyện bình thường, còn một giáo viên có hai "Lớp 8A" thì chính họ cũng không phân biệt được.
        if (await _unitOfWork.StudentGroups.IsNameTakenAsync(name, teacherId, null, cancellationToken))
        {
            return Result.Conflict<StudentGroupDto>("Tên nhóm lớp đã tồn tại.");
        }

        var group = new StudentGroup
        {
            Name = name,
            Description = NormalizeOptional(request.Description),
            OwnerTeacherId = teacherId
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
        if (await _unitOfWork.StudentGroups.IsNameTakenAsync(name, group.OwnerTeacherId, group.Id, cancellationToken))
        {
            return Result.Conflict<StudentGroupDto>("Tên nhóm lớp đã tồn tại.");
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

        foreach (var student in found)
        {
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

    private const string TeacherOnlyMessage = "Chỉ giáo viên mới quản lý được nhóm lớp.";

    /// <summary>
    /// Phạm vi đọc: giáo viên chỉ thấy nhóm của mình, quản trị viên thấy tất cả (NULL).
    /// </summary>
    private int? OwnerScope() => _currentUser.IsInRole(UserRole.Admin) ? null : _currentUser.UserId;

    /// <summary>
    /// Nạp nhóm cho thao tác <b>đọc</b>. Trả NULL khi nhóm không tồn tại <i>hoặc</i> không thuộc
    /// phạm vi người gọi — chỗ gọi biến cả hai thành 404 (§4.2).
    /// </summary>
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

        return _currentUser.UserId is { } userId && group.OwnerTeacherId == userId ? group : null;
    }

    /// <summary>
    /// Nạp nhóm cho thao tác <b>ghi</b>. Chỉ chính chủ mới qua được — kể cả quản trị viên, vì §4.4
    /// xếp quản trị viên vào cột "đọc" ở khu vực nhóm lớp. Tầng Api đã chặn bằng
    /// <c>[Authorize(Roles = Teacher)]</c>; lớp kiểm ở đây để service đứng vững cả khi được gọi từ
    /// chỗ khác, ví dụ một background job sau này.
    /// </summary>
    private async Task<Result<StudentGroup>> LoadWritableAsync(int id, CancellationToken cancellationToken)
    {
        if (!TryGetTeacherId(out var teacherId))
        {
            return Result.Forbidden<StudentGroup>(TeacherOnlyMessage);
        }

        var group = await _unitOfWork.StudentGroups.GetByIdAsync(id, cancellationToken);

        return group is not null && group.OwnerTeacherId == teacherId
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
