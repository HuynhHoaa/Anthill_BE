using Anthill.Application.Common;
using Anthill.Application.DTOs.Accounts;
using Anthill.Application.DTOs.Students;
using Anthill.Application.Interfaces.Realtime;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

/// <summary>
/// Cài đặt <see cref="IStudentService"/> — bảy endpoint của §4.3.
/// </summary>
/// <remarks>
/// Bốn điều quyết định nằm ở file này:
/// <list type="number">
/// <item>Ba định danh <c>email</c>, <c>username</c>, <c>code</c> được kiểm trùng <b>trước</b> khi
/// ghi, trên chuỗi đã trim và bỏ hoa thường, và trả <c>Conflict</c> — unique index ở database chỉ
/// là hàng rào cuối, một mình nó thì lỗi ra ngoài dưới dạng 500 (§5).</item>
/// <item>Khoá tài khoản và reset mật khẩu đều <b>xoay <c>SecurityStamp</c></b>. Không xoay thì
/// token đã cấp vẫn sống tới lúc tự hết hạn, và "khoá tài khoản" chỉ là một cái nhãn (§5).</item>
/// <item>Hai con số thống kê lấy theo <b>lô</b> cho cả trang danh sách, không phải từng em một —
/// hai mươi học sinh mà hỏi riêng thì thành bốn mươi truy vấn.</item>
/// <item>Endpoint nào cũng chỉ nhìn thấy tài khoản có <c>Role = Student</c>. Id của giáo viên
/// truyền vào đây trả <c>NotFound</c>, không phải sửa nhầm người.</item>
/// <item>Hai đường <b>đọc</b> mở cho giáo viên bị giới hạn trong phạm vi của họ
/// (<see cref="TeacherStudentScope"/>): danh sách chỉ gồm lớp mình dạy, hồ sơ mở thêm được cho em
/// đã nhận đề của mình. Ngoài phạm vi là <c>NotFound</c> như id không tồn tại.</item>
/// </list>
/// </remarks>
public sealed class StudentService : IStudentService
{
    private const string NotFoundMessage = "Không tìm thấy học sinh.";

    private const string GroupChangeViaUpdateMessage =
        "Thêm hoặc gỡ lớp không làm được ở form sửa hồ sơ. Hãy vào trang lớp để xếp thêm em, hoặc dùng Gỡ khỏi lớp / Chuyển lớp, kèm lý do.";

    private const string StatusChangeViaUpdateMessage =
        "Khoá hoặc mở khoá tài khoản không làm được ở form sửa hồ sơ. Hãy dùng nút Khoá tài khoản, kèm lý do.";

    private const string LockReasonRequiredMessage = "Vui lòng nhập lý do khoá tài khoản.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    /// <summary>
    /// Hai việc: quyết định phạm vi đọc của giáo viên, và ghi lại ai đã yêu cầu xoá. Phân quyền theo
    /// vai trò (ai được gọi endpoint nào) vẫn nằm ở tầng Api.
    /// </summary>
    private readonly ICurrentUser _currentUser;

    /// <summary>Cắt kết nối realtime đang mở mỗi khi một lối ghi ở đây xoay <c>SecurityStamp</c>.</summary>
    private readonly IUserConnectionCloser _connections;

    /// <summary>Chặn thao tác tự phá trên ba tài khoản demo dùng chung (§H19).</summary>
    private readonly IDemoAccountGuard _demoAccountGuard;

    public StudentService(
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
    public async Task<Result<PagedResult<StudentDto>>> ListAsync(
        ListStudentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Giá trị lạ bị từ chối chứ không âm thầm bỏ lọc: trả về danh sách rộng hơn người dùng yêu
        // cầu mà vẫn kèm mã 200 là kiểu sai không ai phát hiện ra.
        if (!StudentMappingExtensions.TryParseStatusFilter(query.Status, out var status))
        {
            return Result.Failure<PagedResult<StudentDto>>(
                "Bộ lọc trạng thái chỉ nhận 'active', 'locked', 'pendingDeletion', 'deleted' hoặc 'all'.");
        }

        // Hai đường tách hẳn nhau, cùng khuôn với danh sách lớp ở StudentGroupService. Mặc định là
        // đường HẸP: ai không phải quản trị viên đều bị giới hạn theo lớp mình dạy, nên thêm một vai
        // trò mới vào [Authorize] mà quên sửa ở đây thì kết quả là thấy ít đi, không phải thấy hết.
        var page = _currentUser.IsInRole(UserRole.Admin)
            ? await _unitOfWork.Users.GetStudentsPagedAsync(
                query.Search,
                query.GroupId,
                status,
                query,
                cancellationToken)
            : await _unitOfWork.Users.GetStudentsPagedForTeacherAsync(
                _currentUser.RequireUserId(),
                query.Search,
                query.GroupId,
                status,
                query,
                cancellationToken);

        // Một truy vấn GROUP BY cho cả trang thay vì hai truy vấn mỗi học sinh.
        var stats = await _unitOfWork.ExamAttempts.GetStatsByStudentsAsync(
            page.Items.Select(student => student.Id),
            cancellationToken);

        var visibleGroupIds = await VisibleGroupIdsAsync(cancellationToken);
        var items = page.Map(student => ToDto(student, stats, visibleGroupIds));

        return Result.Success(items);
    }

    /// <inheritdoc />
    public async Task<Result<StudentDto>> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        // Cả hai đường đều nạp kèm Groups nên các lớp của em có sẵn, không phải truy vấn thêm.
        //
        // Giáo viên: em ngoài phạm vi trả đúng câu "không tìm thấy" của id không tồn tại, không phải
        // 403 — 403 là xác nhận rằng id đó là một học sinh có thật (§4.2). Phạm vi ở đây rộng hơn
        // danh sách một vế (em đã nhận đề của giáo viên này), để liên kết từ trang kết quả vẫn mở
        // được hồ sơ của em đã chuyển lớp; lý do nằm ở TeacherStudentScope.CanReadProfile.
        var student = _currentUser.IsInRole(UserRole.Admin)
            ? await _unitOfWork.Users.GetProfileAsync(id, cancellationToken)
            : await _unitOfWork.Users.GetStudentProfileForTeacherAsync(
                id,
                _currentUser.RequireUserId(),
                cancellationToken);
        if (!IsStudent(student))
        {
            return Result.NotFound<StudentDto>(NotFoundMessage);
        }

        return Result.Success(await BuildDtoAsync(student!, groups: null, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<StudentDto>> CreateAsync(
        CreateStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Trim một lần ở đây rồi dùng chuỗi đã trim cho cả phần so trùng lẫn phần ghi. Trim ở chỗ
        // này mà so trùng ở chuỗi gốc là đúng cái lỗ hổng §5 chỉ ra: 'HS1001 ' và 'HS1001' cùng lọt.
        var fields = StudentFields.From(request.FullName, request.Email, request.Username, request.Code);

        var conflict = await FindIdentityConflictAsync(fields, excludeUserId: null, cancellationToken);
        if (conflict is not null)
        {
            return Result.Conflict<StudentDto>(conflict);
        }

        var groupResult = await ResolveGroupAsync(request.GroupId, cancellationToken);
        if (groupResult.IsFailure)
        {
            return Result.Failure<StudentDto>(groupResult.Error!, groupResult.ErrorCode);
        }

        var joinedGroup = groupResult.Value;
        if (joinedGroup is { IsLocked: true })
        {
            return Result.Conflict<StudentDto>(
                $"Lớp {joinedGroup.Name} đã khoá nên không xếp thêm em vào được. Hãy mở khoá lớp trước.");
        }

        var student = new User
        {
            FullName = fields.FullName,
            Email = fields.Email,
            Username = fields.Username,
            Code = fields.Code,
            Phone = Normalize(request.Phone),
            PasswordHash = _passwordHasher.Hash(request.Password),
            SecurityStamp = NewSecurityStamp(),
            Role = UserRole.Student,
            Status = ParseStatusOrDefault(request.Status, UserStatus.Active),
            AvatarColor = AvatarPalette.ForKey(fields.Username),

            // §5: mật khẩu do quản trị viên cấp thì ép đổi ở lần đăng nhập đầu. Người tạo tài khoản
            // biết mật khẩu của người dùng là chuyện chấp nhận được trong đúng khoảng thời gian đó
            // và không lâu hơn.
            MustChangePassword = true

            // CreatedAt do BaseEntity đặt bằng DateTime.UtcNow — không nhận từ client (§5).
        };

        if (joinedGroup is not null)
        {
            // Em vào lớp ngay khi tạo: một dòng thành viên và một dòng lịch sử, cùng lần lưu với tài khoản.
            student.GroupMemberships.Add(new StudentGroupMember { StudentGroupId = joinedGroup.Id, JoinedAt = DateTime.UtcNow });

            await _unitOfWork.AccountChanges.AddAsync(
                new AccountChange
                {
                    Account = student,
                    Kind = AccountChangeKind.AddedToGroup,
                    ToGroupId = joinedGroup.Id,
                    ToGroupName = joinedGroup.Name,
                    ActorUserId = _currentUser.UserId
                },
                cancellationToken);
        }

        await _unitOfWork.Users.AddAsync(student, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Tài khoản vừa tạo chưa làm bài nào nên khỏi hỏi bảng thống kê.
        return Result.Success(student.ToStudentDto(
            completedExams: 0,
            averageScore: 0m,
            startedExams: 0,
            groups: joinedGroup is null ? Array.Empty<StudentGroup>() : new[] { joinedGroup },
            includePhone: true));
    }

    /// <inheritdoc />
    public async Task<Result<StudentDto>> UpdateAsync(
        int id,
        UpdateStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Khoá dòng tài khoản tới lúc commit, vì request này có thể khoá tài khoản (xem
        // IUserRepository.LockForUpdateAsync). Entity khoá xong vẫn do DbContext theo dõi, nên
        // SaveChanges chỉ ghi đúng các cột gán dưới đây. Các nhánh trả về sớm chưa ghi gì: rời khối
        // using là transaction tự cuộn lại.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var student = await _unitOfWork.Users.LockForUpdateAsync(id, cancellationToken);
        if (!IsStudent(student))
        {
            return Result.NotFound<StudentDto>(NotFoundMessage);
        }

        var account = student!;

        if (RetiredAccountError(account) is { } retired)
        {
            return Result.Conflict<StudentDto>(retired);
        }

        // §H19: sửa hồ sơ có thể đổi email/username/code hoặc khoá tài khoản demo — chặn cả khối,
        // không chỉ nhánh đổi trạng thái, vì PUT này ghi đè toàn bộ định danh trong một lần.
        if (_demoAccountGuard.IsProtected(account))
        {
            return Result.Forbidden<StudentDto>(DemoAccountMessages.MutationBlocked);
        }

        var fields = StudentFields.From(request.FullName, request.Email, request.Username, request.Code);

        // Loại trừ chính bản ghi đang sửa, nếu không lưu lại hồ sơ mà không đổi gì cũng tự báo trùng.
        var conflict = await FindIdentityConflictAsync(fields, account.Id, cancellationToken);
        if (conflict is not null)
        {
            return Result.Conflict<StudentDto>(conflict);
        }

        // Lớp và trạng thái không đổi được qua đường sửa hồ sơ: gỡ khỏi lớp và khoá tài khoản bắt buộc kèm lý do, mà một PUT ghi
        // đè cả hồ sơ thì không chỗ nào để nhận lý do. Không gửi trường nào thì coi như giữ nguyên; gửi đúng giá trị đang có
        // (một trong các lớp em đang học, hoặc 0 khi em chưa học lớp nào) là hợp lệ để form cũ không vỡ.
        if (request.GroupId is not null)
        {
            var groupResult = await ResolveGroupAsync(request.GroupId, cancellationToken);
            if (groupResult.IsFailure)
            {
                return Result.Failure<StudentDto>(groupResult.Error!, groupResult.ErrorCode);
            }

            var current = await _unitOfWork.StudentGroups.GetByStudentAsync(account.Id, cancellationToken);
            var matches = groupResult.Value is { } requested
                ? current.Any(group => group.Id == requested.Id)
                : current.Count == 0;

            if (!matches)
            {
                return Result.Failure<StudentDto>(GroupChangeViaUpdateMessage);
            }
        }

        if (ParseStatusOrDefault(request.Status, account.Status) != account.Status)
        {
            return Result.Failure<StudentDto>(StatusChangeViaUpdateMessage);
        }

        account.FullName = fields.FullName;
        account.Email = fields.Email;
        account.Username = fields.Username;
        account.Code = fields.Code;
        // Không gửi Phone thì giữ nguyên số đang có (client cũ chưa biết trường này); chuỗi rỗng là xoá trắng.
        if (request.Phone is not null)
        {
            account.Phone = Normalize(request.Phone);
        }
        account.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(await BuildDtoAsync(
            account,
            await ReadGroupsAsync(account.Id, cancellationToken),
            cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<StudentDto>> SetStatusAsync(
        int id,
        UserStatus status,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        // Validator đã chặn ở tầng Api; kiểm lại ở đây để service đứng vững cả khi được gọi từ chỗ khác. Khoá mà
        // không có lý do là điều không được phép xảy ra ở bất kỳ đâu.
        if (status == UserStatus.Locked && string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<StudentDto>(LockReasonRequiredMessage);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var student = await _unitOfWork.Users.LockForUpdateAsync(id, cancellationToken);
        if (!IsStudent(student))
        {
            return Result.NotFound<StudentDto>(NotFoundMessage);
        }

        var account = student!;

        if (RetiredAccountError(account) is { } retired)
        {
            return Result.Conflict<StudentDto>(retired);
        }

        // §H19: khoá/mở khoá tài khoản demo dùng chung qua đường công khai — chặn 403.
        if (_demoAccountGuard.IsProtected(account))
        {
            return Result.Forbidden<StudentDto>(DemoAccountMessages.MutationBlocked);
        }

        if (account.Status != status)
        {
            var stampBefore = account.SecurityStamp;
            ApplyStatus(account, status);
            account.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Users.Update(account);

            // Ai khoá, lúc nào, vì sao — để người xem hồ sơ sau này hiểu. Mở khoá không mang lý do.
            await _unitOfWork.AccountChanges.AddAsync(
                NewChange(
                    account.Id,
                    status == UserStatus.Locked ? AccountChangeKind.Locked : AccountChangeKind.Unlocked,
                    status == UserStatus.Locked ? reason!.Trim() : null),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            CloseConnectionsIfStampRotated(account, stampBefore);
        }

        // Đặt lại đúng trạng thái đang có không phải lỗi: giáo viên bấm nhầm hai lần, hoặc hai tab
        // cùng gửi lệnh khoá, đều nên nhận về cùng một câu trả lời thành công.
        return Result.Success(await BuildDtoAsync(
            account,
            await ReadGroupsAsync(account.Id, cancellationToken),
            cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<StudentDto>> RequestDeletionAsync(
        int id,
        RequestDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var student = await _unitOfWork.Users.LockForUpdateAsync(id, cancellationToken);
        if (!IsStudent(student))
        {
            return Result.NotFound<StudentDto>(NotFoundMessage);
        }

        var account = student!;

        // §H19: yêu cầu xoá tài khoản demo dùng chung — chặn trước khi đụng tới trạng thái.
        if (_demoAccountGuard.IsProtected(account))
        {
            return Result.Forbidden<StudentDto>(DemoAccountMessages.MutationBlocked);
        }

        if (account.Status == UserStatus.Deleted)
        {
            return Result.Conflict<StudentDto>("Tài khoản này đã bị xoá dữ liệu, không thao tác được nữa.");
        }

        if (account.Status == UserStatus.PendingDeletion)
        {
            return Result.Conflict<StudentDto>("Tài khoản này đã nằm trong danh sách chờ xoá.");
        }

        var now = DateTime.UtcNow;

        account.StatusBeforeDeletion = account.Status;
        account.Status = UserStatus.PendingDeletion;
        account.DeletionReason = request.Reason.Trim();
        account.DeletionRequestedAt = now;
        account.DeletionRequestedByUserId = _currentUser.UserId;
        account.UpdatedAt = now;

        // Xoay SecurityStamp để token đang cầm chết ngay, giống hệt nhánh khoá tài khoản. Không
        // xoay thì một em vừa bị đưa vào diện xoá vẫn làm bài tiếp được cho tới khi token hết hạn.
        account.SecurityStamp = Guid.NewGuid().ToString("N");

        _unitOfWork.Users.Update(account);

        // Lý do còn nằm ở cột DeletionReason của tài khoản, nhưng cột đó bị xoá khi khôi phục; dòng lịch sử thì ở lại.
        await _unitOfWork.AccountChanges.AddAsync(
            NewChange(account.Id, AccountChangeKind.DeletionRequested, account.DeletionReason),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _connections.CloseConnectionsOf(account.Id);

        return Result.Success(await BuildDtoAsync(
            account,
            await ReadGroupsAsync(account.Id, cancellationToken),
            cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<StudentDto>> RestoreAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        // Khoá dòng để không chạy chéo với tiến trình xoá: nó bỏ qua dòng đang bị khoá, còn nếu nó
        // khoá trước thì lần đọc này chờ rồi thấy trạng thái Deleted. Không bao giờ có chuyện khôi
        // phục xong rồi mới bị ẩn danh hoá đè lên.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var student = await _unitOfWork.Users.LockForUpdateAsync(id, cancellationToken);
        if (!IsStudent(student))
        {
            return Result.NotFound<StudentDto>(NotFoundMessage);
        }

        var account = student!;

        // Sau khi dữ liệu cá nhân đã bị xoá thì không khôi phục được nữa — không còn gì để khôi
        // phục. Nói thẳng điều đó thay vì trả về thành công trên một tài khoản rỗng.
        if (account.Status == UserStatus.Deleted)
        {
            return Result.Conflict<StudentDto>(
                "Dữ liệu của tài khoản này đã bị xoá vĩnh viễn, không khôi phục được.");
        }

        if (account.Status != UserStatus.PendingDeletion)
        {
            return Result.Conflict<StudentDto>("Tài khoản này không nằm trong danh sách chờ xoá.");
        }

        var now = DateTime.UtcNow;

        // Về đúng trạng thái trước khi bị đưa vào diện xoá. Em đang bị khoá thì vẫn bị khoá; mở lại là
        // việc của lệnh mở khoá. Bản ghi cũ chưa có cột này thì coi như đang hoạt động.
        account.Status = account.StatusBeforeDeletion == UserStatus.Locked
            ? UserStatus.Locked
            : UserStatus.Active;
        account.StatusBeforeDeletion = null;
        account.UpdatedAt = now;

        // Xoá dấu vết yêu cầu: giữ lại thì lần xoá sau sẽ mang theo lý do của lần trước, và tiến
        // trình nền vẫn thấy một mốc thời gian đang đếm.
        account.DeletionReason = null;
        account.DeletionRequestedAt = null;
        account.DeletionRequestedByUserId = null;

        _unitOfWork.Users.Update(account);

        await _unitOfWork.AccountChanges.AddAsync(
            NewChange(account.Id, AccountChangeKind.Restored, reason: null),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(await BuildDtoAsync(
            account,
            await ReadGroupsAsync(account.Id, cancellationToken),
            cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<AccountChangeDto>>> ListChangesAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var student = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        if (!IsStudent(student))
        {
            return Result.NotFound<IReadOnlyList<AccountChangeDto>>(NotFoundMessage);
        }

        var rows = await _unitOfWork.AccountChanges.GetByAccountAsync(id, cancellationToken);

        return Result.Success<IReadOnlyList<AccountChangeDto>>(rows.Select(change => change.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<StudentExamDto>>> ListExamsAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var student = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        if (!IsStudent(student))
        {
            return Result.NotFound<IReadOnlyList<StudentExamDto>>(NotFoundMessage);
        }

        var asOf = DateTime.UtcNow;
        var assignments = await _unitOfWork.ExamAssignments.GetForStudentAsync(id, cancellationToken);

        return Result.Success<IReadOnlyList<StudentExamDto>>(
            assignments.Select(assignment => assignment.ToStudentExamDto(asOf)).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<int>> PurgeDueAccountsAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            return Result.Success(0);
        }

        var now = DateTime.UtcNow;

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var due = await _unitOfWork.Users.LockDueForDeletionAsync(
            now.AddDays(-StudentAnonymizer.RetentionDays),
            batchSize,
            cancellationToken);

        if (due.Count == 0)
        {
            return Result.Success(0);
        }

        var purged = new List<int>(due.Count);

        foreach (var account in due)
        {
            // Kiểm lại lần nữa trên chính bản ghi vừa đọc. Dòng đã bị khoá nên không ai khôi phục chen
            // vào được nữa, nhưng ẩn danh hoá là thao tác không hoàn tác được — một câu hỏi lại trong
            // bộ nhớ rẻ hơn nhiều so với tin tuyệt đối vào câu SQL phía trên.
            if (!StudentAnonymizer.IsDue(account, now))
            {
                continue;
            }

            // Đọc TRƯỚC khi ẩn danh hoá đè lên: đây là tên thật cuối cùng còn dùng được để xoá khỏi
            // thông báo cũ của giáo viên — sau dòng Anonymize thì FullName đã là tên chung chung rồi.
            var previousFullName = account.FullName;

            var token = await FindFreeAnonymousTokenAsync(account.Id, cancellationToken);

            StudentAnonymizer.Anonymize(account, now, token);
            _unitOfWork.Users.Update(account);

            // Rời khỏi mọi lớp: sĩ số các lớp phải phản ánh số em đang thật sự học ở đó.
            _unitOfWork.StudentGroups.RemoveMembers(
                await _unitOfWork.StudentGroups.GetMembershipsOfStudentAsync(account.Id, cancellationToken));
            purged.Add(account.Id);

            await RedactSubmissionNotificationsAsync(account.Id, previousFullName, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Tài khoản chờ xoá đã bị cắt kết nối từ lúc được đưa vào diện xoá, nên thường không còn gì để
        // cắt. Vẫn gọi: ẩn danh hoá cũng xoay stamp, và mọi lối xoay stamp đi theo cùng một quy tắc.
        foreach (var userId in purged)
        {
            _connections.CloseConnectionsOf(userId);
        }

        return Result.Success(purged.Count);
    }

    /// <summary>
    /// Chọn định danh thay thế chưa ai dùng cho một tài khoản sắp bị ẩn danh hoá.
    /// </summary>
    /// <remarks>
    /// Dạng gốc <c>deleted-{id}</c> không va với ai nếu không có người cố tình đặt trùng, nhưng tên đăng
    /// nhập, mã và email do người dùng tự nhập. Một lần va là vi phạm chỉ mục duy nhất, cả lô một trăm
    /// tài khoản bị cuộn lại, và lượt quét sáu tiếng sau lại chọn đúng lô đó rồi hỏng y như cũ.
    /// </remarks>
    private Task<string> FindFreeAnonymousTokenAsync(int userId, CancellationToken cancellationToken) =>
        // Ba cột là một không gian tên khi kiểm trùng (xem UserRepository), nên một lượt hỏi theo tên
        // đăng nhập đã soi cả email lẫn mã; email thay thế có đuôi riêng nên phải hỏi thêm.
        StudentAnonymizer.ChooseFreeTokenAsync(
            userId,
            async token =>
                await _unitOfWork.Users.IsUsernameTakenAsync(token, userId, cancellationToken)
                || await _unitOfWork.Users.IsEmailTakenAsync(
                    StudentAnonymizer.EmailFor(token), userId, cancellationToken));

    /// <summary>
    /// Dọn tên thật khỏi thông báo nộp bài cũ của giáo viên khi xoá một học sinh (§5, H22).
    /// </summary>
    /// <remarks>
    /// Không gọi <see cref="StudentAnonymizer.RedactSubmissionMessage"/> trực tiếp từ
    /// <see cref="PurgeDueAccountsAsync"/>: tách riêng để bước tìm-thông-báo (đi database) và bước
    /// xoá-tên (thuần, đã có unit test ở <c>StudentAnonymizerTests</c>) không lẫn vào nhau trong cùng
    /// một vòng lặp dài.
    /// </remarks>
    private async Task RedactSubmissionNotificationsAsync(
        int studentId,
        string previousFullName,
        CancellationToken cancellationToken)
    {
        var notifications = await _unitOfWork.Notifications.GetByAttemptOwnerAsync(studentId, cancellationToken);

        foreach (var notification in notifications)
        {
            StudentAnonymizer.RedactSubmissionMessage(notification, studentId, previousFullName);
        }
    }

    /// <inheritdoc />
    public async Task<Result<ResetPasswordResponse>> ResetPasswordAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var student = await _unitOfWork.Users.LockForUpdateAsync(id, cancellationToken);
        if (!IsStudent(student))
        {
            return Result.NotFound<ResetPasswordResponse>(NotFoundMessage);
        }

        var account = student!;

        if (RetiredAccountError(account) is { } retired)
        {
            return Result.Conflict<ResetPasswordResponse>(retired);
        }

        // §H19: cấp lại mật khẩu của tài khoản demo dùng chung — chặn 403, cùng lý do với các nhánh
        // khác của file này.
        if (_demoAccountGuard.IsProtected(account))
        {
            return Result.Forbidden<ResetPasswordResponse>(DemoAccountMessages.MutationBlocked);
        }

        var password = PasswordGenerator.Generate();

        account.PasswordHash = _passwordHasher.Hash(password);

        // Xoay stamp: mật khẩu cũ vừa bị vô hiệu hoá, nên mọi phiên mở bằng mật khẩu cũ cũng phải
        // chết theo. Không xoay thì người đang giữ token vẫn dùng tiếp bình thường — mà tình huống
        // điển hình của reset là tài khoản nghi bị người khác đăng nhập.
        account.SecurityStamp = NewSecurityStamp();
        account.MustChangePassword = true;
        account.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _connections.CloseConnectionsOf(account.Id);

        return Result.Success(new ResetPasswordResponse
        {
            StudentId = account.Id,
            Password = password,
            MustChangePassword = true
        });
    }

    /// <inheritdoc />
    public async Task<Result<AvailabilityResponse>> CheckAvailabilityAsync(
        AvailabilityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.IsEmpty)
        {
            return Result.Failure<AvailabilityResponse>(
                "Cần ít nhất một trong ba giá trị: email, username hoặc mã học sinh.");
        }

        bool? emailAvailable = null;
        bool? usernameAvailable = null;
        bool? codeAvailable = null;

        if (Normalize(query.Email) is { } email)
        {
            emailAvailable = !await _unitOfWork.Users.IsEmailTakenAsync(email, query.ExcludeStudentId, cancellationToken);
        }

        if (Normalize(query.Username) is { } username)
        {
            usernameAvailable = !await _unitOfWork.Users.IsUsernameTakenAsync(username, query.ExcludeStudentId, cancellationToken);
        }

        if (Normalize(query.Code) is { } code)
        {
            codeAvailable = !await _unitOfWork.Users.IsCodeTakenAsync(code, query.ExcludeStudentId, cancellationToken);
        }

        return Result.Success(new AvailabilityResponse
        {
            EmailAvailable = emailAvailable,
            UsernameAvailable = usernameAvailable,
            CodeAvailable = codeAvailable,

            // Trường không được hỏi không kéo kết quả xuống false — chỉ những trường đã hỏi mới tính.
            AllAvailable = emailAvailable is not false
                           && usernameAvailable is not false
                           && codeAvailable is not false
        });
    }

    /// <summary>
    /// Kiểm cả ba định danh, trả về câu giải thích cho định danh đầu tiên bị trùng, hoặc NULL khi
    /// không trùng gì.
    /// </summary>
    /// <remarks>
    /// Dừng ở lỗi đầu tiên chứ không gom cả ba: <c>message</c> là thứ frontend hiện lên toast, mà
    /// một người dùng sửa từng ô một thì đọc từng câu một cũng đủ.
    /// </remarks>
    private async Task<string?> FindIdentityConflictAsync(
        StudentFields fields,
        int? excludeUserId,
        CancellationToken cancellationToken)
    {
        if (await _unitOfWork.Users.IsEmailTakenAsync(fields.Email, excludeUserId, cancellationToken))
        {
            return "Email này đã được một tài khoản khác sử dụng.";
        }

        if (await _unitOfWork.Users.IsUsernameTakenAsync(fields.Username, excludeUserId, cancellationToken))
        {
            return "Tên đăng nhập này đã tồn tại.";
        }

        if (await _unitOfWork.Users.IsCodeTakenAsync(fields.Code, excludeUserId, cancellationToken))
        {
            return "Mã học sinh này đã tồn tại.";
        }

        return null;
    }

    /// <summary>
    /// Đổi <c>groupId</c> client gửi thành lớp thật, sau khi xác minh lớp có thật. <c>null</c> hay <c>0</c> là không chọn lớp.
    /// </summary>
    /// <remarks>
    /// Không kiểm thì một id lạ đi thẳng xuống database và vỡ ở khoá ngoại thành lỗi 500. Trả
    /// <c>Validation</c> chứ không phải <c>NotFound</c>: thứ không tìm thấy là <b>một tham số</b>
    /// bên trong request, không phải bản thân tài nguyên mà URL trỏ tới.
    /// </remarks>
    private async Task<Result<StudentGroup?>> ResolveGroupAsync(int? clientGroupId, CancellationToken cancellationToken)
    {
        var groupId = StudentGroupDefaults.ToStoredGroupId(clientGroupId);
        if (groupId is null)
        {
            return Result.Success<StudentGroup?>(null);
        }

        var group = await _unitOfWork.StudentGroups.GetByIdAsync(groupId.Value, cancellationToken);

        return group is not null
            ? Result.Success<StudentGroup?>(group)
            : Result.Failure<StudentGroup?>("Nhóm lớp không tồn tại.");
    }

    /// <summary>
    /// Đặt trạng thái, và xoay <c>SecurityStamp</c> khi <b>khoá</b>.
    /// </summary>
    /// <remarks>
    /// Chỉ xoay ở chiều khoá. Mở khoá lại thì tài khoản đang không có phiên nào hợp lệ để đá ra,
    /// và xoay thêm một lần nữa chỉ khiến học sinh vừa được mở khoá phải đăng nhập lại lần thứ hai.
    /// </remarks>
    private static void ApplyStatus(User account, UserStatus status)
    {
        if (account.Status == status)
        {
            return;
        }

        account.Status = status;

        if (status == UserStatus.Locked)
        {
            account.SecurityStamp = NewSecurityStamp();
        }
    }

    /// <summary>
    /// Cắt kết nối realtime của tài khoản nếu lần ghi vừa commit đã xoay stamp. Gọi sau commit, xem
    /// <see cref="IUserConnectionCloser"/>.
    /// </summary>
    /// <remarks>
    /// So stamp thay vì so trạng thái: quy tắc "khi nào xoay" nằm ở <see cref="ApplyStatus"/>, và hỏi thẳng
    /// kết quả của nó thì hai nơi không thể hiểu khác nhau.
    /// </remarks>
    private void CloseConnectionsIfStampRotated(User account, string stampBefore)
    {
        if (!string.Equals(account.SecurityStamp, stampBefore, StringComparison.Ordinal))
        {
            _connections.CloseConnectionsOf(account.Id);
        }
    }

    /// <summary>
    /// Dựng DTO cho một học sinh, kèm hai con số thống kê lấy trong một truy vấn. <paramref name="groups"/> là các lớp của em khi
    /// chỗ gọi không nạp kèm <c>Groups</c> (đường ghi nạp tài khoản trần).
    /// </summary>
    private async Task<StudentDto> BuildDtoAsync(
        User student,
        IReadOnlyCollection<StudentGroup>? groups,
        CancellationToken cancellationToken)
    {
        var stats = await _unitOfWork.ExamAttempts.GetStatsByStudentsAsync([student.Id], cancellationToken);

        return ToDto(student, stats, await VisibleGroupIdsAsync(cancellationToken), groups);
    }

    private StudentDto ToDto(
        User student,
        IReadOnlyDictionary<int, (int Completed, decimal AverageScore, int Started, int Assigned)> stats,
        IReadOnlySet<int>? visibleGroupIds,
        IReadOnlyCollection<StudentGroup>? groups = null)
    {
        // Học sinh chưa có lượt làm bài nào có thể vắng mặt trong bảng thống kê — đó là (0, 0, 0),
        // không phải lỗi.
        var (completed, averageScore, started, assigned) = stats.TryGetValue(student.Id, out var row) ? row : (0, 0m, 0, 0);

        // Số điện thoại do nhà trường quản lý: chỉ quản trị viên nhận trường này.
        return student.ToStudentDto(
            completed,
            averageScore,
            started,
            groups,
            visibleGroupIds,
            includePhone: _currentUser.IsInRole(UserRole.Admin),
            assignedExams: assigned);
    }

    /// <summary>
    /// Các lớp giáo viên này được biết (lớp mình dạy, đã công bố); <c>null</c> với quản trị viên — không giới hạn. Lớp của một
    /// đồng nghiệp mà em cùng học không phải chuyện giáo viên này được thấy.
    /// </summary>
    private async Task<IReadOnlySet<int>?> VisibleGroupIdsAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.IsInRole(UserRole.Admin))
        {
            return null;
        }

        var mine = await _unitOfWork.StudentGroups.GetForTeacherAsync(
            _currentUser.RequireUserId(),
            search: null,
            cancellationToken);

        return mine.Select(group => group.Id).ToHashSet();
    }

    /// <summary>Dựng một dòng lịch sử của tài khoản (khoá, mở khoá, xoá, khôi phục) do quản trị viên hiện tại thực hiện.</summary>
    private AccountChange NewChange(int studentId, AccountChangeKind kind, string? reason) =>
        new()
        {
            AccountId = studentId,
            Kind = kind,
            Reason = reason,
            ActorUserId = _currentUser.UserId
        };

    private async Task<IReadOnlyList<StudentGroup>> ReadGroupsAsync(int studentId, CancellationToken cancellationToken) =>
        await _unitOfWork.StudentGroups.GetByStudentAsync(studentId, cancellationToken);

    /// <summary>
    /// Endpoint học sinh chỉ được nhìn thấy tài khoản học sinh. Id của giáo viên hay quản trị viên
    /// truyền vào trả về "không tìm thấy" — vừa đúng nghĩa, vừa không xác nhận id đó có thật (§4.2).
    /// </summary>
    private static bool IsStudent(User? user) => user is { Role: UserRole.Student };

    /// <summary>
    /// Câu từ chối khi tài khoản đang ở diện xoá, hoặc NULL nếu được phép thao tác.
    /// </summary>
    /// <remarks>
    /// Siết bộ phân tích trạng thái thôi chưa đủ: <c>active</c> là giá trị hợp lệ, nên PATCH status
    /// đặt <c>active</c> lên một tài khoản chờ xoá vẫn đi vòng qua bước khôi phục, để lại lý do và
    /// mốc đếm ngược cũ. Nên mọi lối ghi — sửa hồ sơ, đổi trạng thái, cấp lại mật khẩu — đều hỏi ở
    /// đây trước. Lối duy nhất đi được là <see cref="RestoreAsync"/>.
    ///
    /// Cấp lại mật khẩu cho tài khoản chờ xoá cũng vô nghĩa: em đó không đăng nhập được, và mật
    /// khẩu mới chỉ là thêm một bí mật phải giữ cho một tài khoản sắp biến mất.
    /// </remarks>
    private static string? RetiredAccountError(User account) => account.Status switch
    {
        UserStatus.PendingDeletion =>
            "Tài khoản đang chờ xoá. Hãy khôi phục tài khoản trước khi thao tác.",
        UserStatus.Deleted =>
            "Dữ liệu của tài khoản này đã bị xoá vĩnh viễn, không thao tác được nữa.",
        _ => null
    };

    private static UserStatus ParseStatusOrDefault(string? value, UserStatus fallback) =>
        StudentMappingExtensions.TryParseSettableStatus(value, out var parsed) ? parsed : fallback;

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

    /// <summary>
    /// Bốn chuỗi định danh đã trim, gói lại để không có đường nào lỡ dùng chuỗi gốc.
    /// </summary>
    private readonly record struct StudentFields(string FullName, string Email, string Username, string Code)
    {
        public static StudentFields From(string? fullName, string? email, string? username, string? code) => new(
            fullName?.Trim() ?? string.Empty,
            email?.Trim() ?? string.Empty,
            username?.Trim() ?? string.Empty,
            code?.Trim() ?? string.Empty);
    }
}
