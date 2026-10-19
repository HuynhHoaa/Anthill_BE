using Anthill.Application.Common;
using Anthill.Application.DTOs.Accounts;
using Anthill.Application.DTOs.Teachers;
using Anthill.Application.Interfaces.Realtime;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

/// <summary>Cài đặt <see cref="ITeacherService"/>.</summary>
/// <remarks>
/// Cùng khuôn với <see cref="StudentService"/>: định danh kiểm trùng trước khi ghi (chung một không gian tên với username
/// và mã học sinh), khoá và cấp lại mật khẩu đều xoay <c>SecurityStamp</c>, ghi đè thì khoá dòng, tài khoản demo dùng chung
/// được chặn (§H19). Khác ở chỗ giáo viên đăng nhập bằng email nên <c>Username</c> luôn bằng <c>Email</c>.
/// </remarks>
public sealed class TeacherService : ITeacherService
{
    private const string NotFoundMessage = "Không tìm thấy giáo viên.";
    private const string AdminOnlyMessage = "Chỉ quản trị viên mới quản lý tài khoản giáo viên.";
    private const string EmailTakenMessage = "Email này đã được một tài khoản khác sử dụng.";
    private const string SubjectMissingMessage = "Môn học không tồn tại.";
    private const string LockReasonRequiredMessage = "Vui lòng nhập lý do khoá tài khoản.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUser _currentUser;
    private readonly IUserConnectionCloser _connections;
    private readonly IDemoAccountGuard _demoAccountGuard;

    public TeacherService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ICurrentUser currentUser,
        IUserConnectionCloser connections,
        IDemoAccountGuard demoAccountGuard)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _connections = connections;
        _demoAccountGuard = demoAccountGuard;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<TeacherDto>>> ListAsync(
        ListTeachersQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!IsAdmin)
        {
            return Result.Forbidden<PagedResult<TeacherDto>>(AdminOnlyMessage);
        }

        // Giá trị lạ bị từ chối chứ không âm thầm bỏ lọc: lọc sai mà vẫn nhận đủ danh sách thì người dùng tin là đã lọc.
        UserStatus? status = null;
        var rawStatus = string.IsNullOrWhiteSpace(query.Status) ? "all" : query.Status.Trim().ToLowerInvariant();
        if (rawStatus != "all")
        {
            if (!StudentMappingExtensions.TryParseSettableStatus(rawStatus, out var parsed))
            {
                return Result.Failure<PagedResult<TeacherDto>>("Bộ lọc trạng thái chỉ nhận 'active', 'locked' hoặc 'all'.");
            }

            status = parsed;
        }

        var page = await _unitOfWork.Users.GetTeachersPagedAsync(
            query.Search,
            status,
            query.SubjectId,
            query,
            cancellationToken);

        var counts = await _unitOfWork.StudentGroups.CountByTeacherAsync(
            page.Items.Select(teacher => teacher.Id),
            cancellationToken);

        return Result.Success(page.Map(teacher => teacher.ToTeacherDto(counts.GetValueOrDefault(teacher.Id))));
    }

    /// <inheritdoc />
    public async Task<Result<TeacherDto>> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        if (!IsAdmin)
        {
            return Result.Forbidden<TeacherDto>(AdminOnlyMessage);
        }

        var teacher = await _unitOfWork.Users.GetProfileAsync(id, cancellationToken);
        if (!IsTeacher(teacher))
        {
            return Result.NotFound<TeacherDto>(NotFoundMessage);
        }

        return Result.Success(await BuildDetailAsync(teacher!, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<TeacherDto>> CreateAsync(
        CreateTeacherRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsAdmin)
        {
            return Result.Forbidden<TeacherDto>(AdminOnlyMessage);
        }

        // Trim một lần rồi dùng chuỗi đã trim cho cả so trùng lẫn ghi (§5): 'a@x.vn ' và 'a@x.vn' không được cùng lọt.
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim();

        if (await _unitOfWork.Users.IsEmailTakenAsync(email, excludeUserId: null, cancellationToken))
        {
            return Result.Conflict<TeacherDto>(EmailTakenMessage);
        }

        if (await _unitOfWork.Subjects.GetByIdAsync(request.SubjectId, cancellationToken) is null)
        {
            return Result.Failure<TeacherDto>(SubjectMissingMessage);
        }

        var teacher = new User
        {
            FullName = fullName,
            Email = email,
            Username = email,
            Phone = Normalize(request.Phone),
            PasswordHash = _passwordHasher.Hash(request.Password),
            SecurityStamp = NewSecurityStamp(),
            Role = UserRole.Teacher,
            Status = UserStatus.Active,
            SubjectId = request.SubjectId,
            AvatarColor = AvatarPalette.ForKey(email),

            // §5: mật khẩu do quản trị viên cấp thì ép đổi ở lần đăng nhập đầu.
            MustChangePassword = true
        };

        await _unitOfWork.Users.AddAsync(teacher, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Users.GetProfileAsync(teacher.Id, cancellationToken) ?? teacher;

        return Result.Success(created.ToTeacherDto(groupCount: 0));
    }

    /// <inheritdoc />
    public async Task<Result<TeacherDto>> UpdateAsync(
        int id,
        UpdateTeacherRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsAdmin)
        {
            return Result.Forbidden<TeacherDto>(AdminOnlyMessage);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var locked = await _unitOfWork.Users.LockForUpdateAsync(id, cancellationToken);
        if (!IsTeacher(locked))
        {
            return Result.NotFound<TeacherDto>(NotFoundMessage);
        }

        var teacher = locked!;

        // §H19: tài khoản demo dùng chung không sửa được qua đường công khai.
        if (_demoAccountGuard.IsProtected(teacher))
        {
            return Result.Forbidden<TeacherDto>(DemoAccountMessages.MutationBlocked);
        }

        var fullName = request.FullName.Trim();
        var email = request.Email.Trim();

        // Loại trừ chính bản ghi đang sửa, nếu không lưu lại hồ sơ mà không đổi gì cũng tự báo trùng.
        if (await _unitOfWork.Users.IsEmailTakenAsync(email, teacher.Id, cancellationToken))
        {
            return Result.Conflict<TeacherDto>(EmailTakenMessage);
        }

        if (request.SubjectId != teacher.SubjectId)
        {
            if (await _unitOfWork.Subjects.GetByIdAsync(request.SubjectId, cancellationToken) is null)
            {
                return Result.Failure<TeacherDto>(SubjectMissingMessage);
            }

            // Ngân hàng câu hỏi và việc sinh đề đều chia theo môn của người soạn. Đổi môn khi đã có câu hỏi hay đề thì chúng
            // nằm lại ở môn cũ mà chính giáo viên không còn thấy, và đề sinh lại sẽ không tìm được câu của mình.
            var hasQuestions = await _unitOfWork.Questions.CountAsync(
                question => question.CreatedByUserId == teacher.Id, cancellationToken) > 0;
            var hasExams = await _unitOfWork.Exams.CountAsync(
                exam => exam.CreatedByUserId == teacher.Id, cancellationToken) > 0;

            if (hasQuestions || hasExams)
            {
                return Result.Conflict<TeacherDto>(
                    "Giáo viên này đã soạn câu hỏi hoặc đề kiểm tra trong môn hiện tại nên chưa đổi môn được.");
            }

            teacher.SubjectId = request.SubjectId;
        }

        teacher.FullName = fullName;
        teacher.Email = email;
        teacher.Username = email;
        if (request.Phone is not null)
        {
            teacher.Phone = Normalize(request.Phone);
        }

        teacher.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(teacher);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var reloaded = await _unitOfWork.Users.GetProfileAsync(teacher.Id, cancellationToken) ?? teacher;

        return Result.Success(await BuildDetailAsync(reloaded, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<TeacherDto>> SetStatusAsync(
        int id,
        UserStatus status,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin)
        {
            return Result.Forbidden<TeacherDto>(AdminOnlyMessage);
        }

        // Validator đã chặn ở tầng Api; kiểm lại để service đứng vững khi được gọi từ chỗ khác.
        if (status == UserStatus.Locked && string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<TeacherDto>(LockReasonRequiredMessage);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var locked = await _unitOfWork.Users.LockForUpdateAsync(id, cancellationToken);
        if (!IsTeacher(locked))
        {
            return Result.NotFound<TeacherDto>(NotFoundMessage);
        }

        var teacher = locked!;

        if (_demoAccountGuard.IsProtected(teacher))
        {
            return Result.Forbidden<TeacherDto>(DemoAccountMessages.MutationBlocked);
        }

        var rotated = false;

        if (teacher.Status != status)
        {
            teacher.Status = status;
            teacher.UpdatedAt = DateTime.UtcNow;

            // Chỉ xoay ở chiều khoá: mở khoá thì tài khoản không có phiên nào hợp lệ để đá ra.
            if (status == UserStatus.Locked)
            {
                teacher.SecurityStamp = NewSecurityStamp();
                rotated = true;
            }

            _unitOfWork.Users.Update(teacher);

            await _unitOfWork.AccountChanges.AddAsync(
                new AccountChange
                {
                    AccountId = teacher.Id,
                    Kind = status == UserStatus.Locked ? AccountChangeKind.Locked : AccountChangeKind.Unlocked,
                    Reason = status == UserStatus.Locked ? reason!.Trim() : null,
                    ActorUserId = _currentUser.UserId
                },
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (rotated)
            {
                _connections.CloseConnectionsOf(teacher.Id);
            }
        }

        // Đặt lại đúng trạng thái đang có không phải lỗi: bấm nhầm hai lần nhận về cùng một câu trả lời thành công.
        var reloaded = await _unitOfWork.Users.GetProfileAsync(teacher.Id, cancellationToken) ?? teacher;

        return Result.Success(await BuildDetailAsync(reloaded, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<TeacherPasswordResponse>> ResetPasswordAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin)
        {
            return Result.Forbidden<TeacherPasswordResponse>(AdminOnlyMessage);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var locked = await _unitOfWork.Users.LockForUpdateAsync(id, cancellationToken);
        if (!IsTeacher(locked))
        {
            return Result.NotFound<TeacherPasswordResponse>(NotFoundMessage);
        }

        var teacher = locked!;

        if (_demoAccountGuard.IsProtected(teacher))
        {
            return Result.Forbidden<TeacherPasswordResponse>(DemoAccountMessages.MutationBlocked);
        }

        var password = PasswordGenerator.Generate();

        teacher.PasswordHash = _passwordHasher.Hash(password);

        // Xoay stamp: mọi phiên mở bằng mật khẩu cũ chết theo — tình huống điển hình của reset là tài khoản nghi bị lộ.
        teacher.SecurityStamp = NewSecurityStamp();
        teacher.MustChangePassword = true;
        teacher.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(teacher);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _connections.CloseConnectionsOf(teacher.Id);

        return Result.Success(new TeacherPasswordResponse
        {
            TeacherId = teacher.Id,
            Password = password,
            MustChangePassword = true
        });
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<AccountChangeDto>>> ListChangesAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin)
        {
            return Result.Forbidden<IReadOnlyList<AccountChangeDto>>(AdminOnlyMessage);
        }

        var teacher = await _unitOfWork.Users.GetProfileAsync(id, cancellationToken);
        if (!IsTeacher(teacher))
        {
            return Result.NotFound<IReadOnlyList<AccountChangeDto>>(NotFoundMessage);
        }

        var rows = await _unitOfWork.AccountChanges.GetByAccountAsync(id, cancellationToken);

        return Result.Success<IReadOnlyList<AccountChangeDto>>(rows.Select(change => change.ToDto()).ToList());
    }

    private bool IsAdmin => _currentUser.IsInRole(UserRole.Admin);

    /// <summary>Endpoint giáo viên chỉ thấy tài khoản giáo viên: id của học sinh hay quản trị viên trả "không tìm thấy".</summary>
    private static bool IsTeacher(User? user) => user is { Role: UserRole.Teacher };

    /// <summary>Dựng DTO chi tiết: kèm các lớp đang phụ trách và sĩ số từng lớp (hai truy vấn gộp, không theo từng lớp).</summary>
    private async Task<TeacherDto> BuildDetailAsync(User teacher, CancellationToken cancellationToken)
    {
        var groups = await _unitOfWork.StudentGroups.GetByTeacherAsync(teacher.Id, cancellationToken);
        var counts = await _unitOfWork.Users.CountStudentsByGroupAsync(groups.Select(group => group.Id), cancellationToken);

        IReadOnlyList<TeacherGroupDto> summary = groups
            .Select(group => new TeacherGroupDto
            {
                Id = group.Id,
                Name = group.Name,
                StudentCount = counts.GetValueOrDefault(group.Id),
                IsPublished = group.IsPublished
            })
            .ToList();

        return teacher.ToTeacherDto(summary.Count, summary);
    }

    private static string? Normalize(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string NewSecurityStamp() => Guid.NewGuid().ToString("N");
}
