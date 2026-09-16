using Anthill.Application.Common;
using Anthill.Application.DTOs.Auth;
using Anthill.Application.Interfaces.Realtime;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Application.Validators;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

/// <summary>
/// Cài đặt <see cref="IAuthService"/> — sáu endpoint của §4.1.
/// </summary>
/// <remarks>
/// Ba điều quyết định nằm ở file này và không được nhân nhượng:
/// <list type="number">
/// <item>Vai trò lấy từ cột <c>User.Role</c>, <b>không</b> từ <c>LoginRequest.Role</c> (§3 quy tắc ⑤).</item>
/// <item>Sai tài khoản và sai mật khẩu trả cùng một thông điệp, cùng một mã lỗi, cùng một khoảng thời gian.</item>
/// <item>Danh tính của bốn thao tác còn lại đọc từ <see cref="ICurrentUser"/>, không nhận id từ client (§3 quy tắc ④).</item>
/// </list>
/// Mọi lỗi nghiệp vụ đi qua <see cref="Result"/> kèm <see cref="ErrorCode"/>; tầng Api dịch sang HTTP.
/// </remarks>
public sealed class AuthService : IAuthService
{
    private const string InvalidCredentialsMessage = "Tên đăng nhập hoặc mật khẩu không đúng.";
    private const string SessionExpiredMessage = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
    private const string RoleMismatchMessage = "Tài khoản không thuộc vai trò đã chọn.";

    /// <summary>Tên hai trường của <see cref="ChangePasswordRequest"/> đúng như trong JSON.</summary>
    private const string CurrentPasswordField = "currentPassword";
    private const string NewPasswordField = "newPassword";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICurrentUser _currentUser;
    private readonly IUserConnectionCloser _connections;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ICurrentUser currentUser,
        IUserConnectionCloser connections)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _currentUser = currentUser;
        _connections = connections;
    }

    /// <inheritdoc />
    public async Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var identifier = request.Username?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (identifier.Length == 0 || password.Length == 0)
        {
            return Result.Unauthorized<LoginResponse>(InvalidCredentialsMessage);
        }

        // Một lần tra cho cả ba dạng định danh: email, username, mã học sinh — không phân biệt hoa
        // thường (§5). Vai trò client gửi lên KHÔNG tham gia điều kiện tra: tra theo vai trò nghĩa là
        // cùng một chuỗi có thể ra hai tài khoản khác nhau tuỳ client khai báo.
        var user = await _unitOfWork.Users.GetByLoginIdentifierAsync(identifier, cancellationToken);

        if (user is null)
        {
            BurnPasswordVerificationTime(password);
            return Result.Unauthorized<LoginResponse>(InvalidCredentialsMessage);
        }

        if (!_passwordHasher.Verify(password, user.PasswordHash))
        {
            return Result.Unauthorized<LoginResponse>(InvalidCredentialsMessage);
        }

        // Từ dòng này trở xuống người gọi đã chứng minh biết mật khẩu, nên thông điệp cụ thể không
        // làm lộ tài khoản nào có thật nữa. Vì vậy hai kiểm tra dưới đây đứng SAU phần đối chiếu mật
        // khẩu: đặt trước sẽ biến "tài khoản bị khoá" thành một cách dò tài khoản tồn tại.
        // Ba trạng thái chặn đăng nhập chứ không phải một. Chỉ kiểm Locked là để lọt hai trạng
        // thái mới của quy trình xoá — một em vừa bị đưa vào diện xoá vẫn vào làm bài như thường,
        // và một tài khoản đã bị xoá sạch dữ liệu vẫn đăng nhập được nếu ai đó đoán ra mật khẩu.
        if (user.Status != UserStatus.Active)
        {
            return Result.Forbidden<LoginResponse>(MessageForBlockedStatus(user.Status));
        }

        // §3 quy tắc ⑤: request.Role chỉ để ĐỐI CHIẾU với vai trò thật. Không khớp thì từ chối,
        // tuyệt đối không "chiều" theo vai trò client chọn.
        if (!MatchesRequestedRole(request.Role, user.Role))
        {
            return Result.Forbidden<LoginResponse>(RoleMismatchMessage);
        }

        // Claim sinh từ entity đọc trong database — đây là chỗ duy nhất vai trò đi vào token.
        var token = _jwtTokenGenerator.GenerateToken(user);
        var profile = await BuildProfileDtoAsync(user, cancellationToken);

        return Result.Success(new LoginResponse
        {
            AccessToken = token.AccessToken,
            ExpiresAt = token.ExpiresAt.ToUtc(),
            // Chưa có bảng lưu refresh token nên không phát hành thứ mà server không kiểm chứng lại
            // được. Trường này NULL đúng như LoginResponse đã lường trước.
            RefreshToken = null,
            User = profile,
            MustChangePassword = user.MustChangePassword
        });
    }

    /// <inheritdoc />
    public Task<Result<LoginResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Domain chưa có entity lưu refresh token, mà LoginAsync cũng không phát hành chuỗi nào, nên
        // mọi giá trị gửi tới đây đều không đối chiếu được với bất cứ thứ gì. Cấp access token dựa
        // trên một chuỗi server không hề biết là tự mở cửa sau; trả 401 để client đăng nhập lại.
        return Task.FromResult(Result.Unauthorized<LoginResponse>(SessionExpiredMessage));
    }

    /// <inheritdoc />
    public async Task<Result<UserProfileDto>> GetCurrentProfileAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Result.Unauthorized<UserProfileDto>(SessionExpiredMessage);
        }

        // Đường đọc: nạp kèm Subject và Group để dựng hồ sơ trong một truy vấn.
        var user = await _unitOfWork.Users.GetProfileAsync(userId, cancellationToken);

        var guard = GuardAccountState(user);
        if (guard.IsFailure)
        {
            return Result.Failure<UserProfileDto>(guard.Error!, guard.ErrorCode);
        }

        return Result.Success(await BuildProfileDtoAsync(user!, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<UserProfileDto>> UpdateCurrentProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetCurrentUserId(out var userId))
        {
            return Result.Unauthorized<UserProfileDto>(SessionExpiredMessage);
        }

        // Đường ghi dùng GetByIdAsync: entity trần do DbContext theo dõi là đủ. Vì nó đang được theo
        // dõi, SaveChanges chỉ ghi đúng các cột gán dưới đây (xem GenericRepository.Update) — lần lưu
        // hồ sơ này không thể ghi đè Status, SecurityStamp hay PasswordHash mà quản trị viên vừa đổi
        // trong lúc request đang chạy, tức là không thể làm một lệnh khoá biến mất. Cố ý không khoá dòng:
        // nếu quản trị viên ghi chen vào giữa lúc đọc và lúc lưu, concurrency token (xmin) biến lần lưu
        // này thành 409 — bên ghi trên bản đã cũ là bên phải làm lại, không phải lệnh khoá.
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

        var guard = GuardAccountState(user);
        if (guard.IsFailure)
        {
            return Result.Failure<UserProfileDto>(guard.Error!, guard.ErrorCode);
        }

        var account = user!;

        var fullName = request.FullName?.Trim() ?? string.Empty;
        if (fullName.Length == 0)
        {
            return Result.Failure<UserProfileDto>("Họ và tên không được để trống.");
        }

        var email = request.Email?.Trim() ?? string.Empty;
        if (!ValidationPatterns.Email.IsMatch(email))
        {
            return Result.Failure<UserProfileDto>("Email không đúng định dạng.");
        }

        var phone = NormalizeOptional(request.Phone);
        if (phone is not null && !ValidationPatterns.Phone.IsMatch(phone))
        {
            return Result.Failure<UserProfileDto>("Số điện thoại không hợp lệ.");
        }

        var school = NormalizeOptional(request.School);

        // So trùng trên chuỗi ĐÃ trim, loại trừ chính mình — nếu không, người dùng lưu lại hồ sơ mà
        // không đổi email sẽ tự báo trùng với chính mình (§5).
        if (await _unitOfWork.Users.IsEmailTakenAsync(email, account.Id, cancellationToken))
        {
            return Result.Conflict<UserProfileDto>("Email này đã được một tài khoản khác sử dụng.");
        }

        // Danh sách trắng của §5 gồm năm trường: fullName, email, phone, school, subject.
        // Bốn trường đầu gán ở đây; trường thứ năm KHÔNG có mặt trong UpdateProfileRequest nên không
        // tồn tại đường nào để đổi — môn của giáo viên do quản trị viên gắn bằng SubjectId, không đổi
        // bằng cách gõ tên môn, và học sinh thì không có môn. Id, Role, Username, Code, GroupId,
        // AvatarColor và CreatedAt cũng vắng mặt khỏi DTO vì cùng một lý do: client gửi lên thì rơi
        // mất ngay lúc deserialize, không cần lọc lại ở đây.
        account.FullName = fullName;
        account.Email = email;
        account.Phone = phone;
        account.School = school;
        account.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await BuildProfileDtoAsync(account, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetCurrentUserId(out var userId))
        {
            return Result.Unauthorized(SessionExpiredMessage);
        }

        // Khoá dòng từ lúc đọc tới lúc commit (xem IUserRepository.LockForUpdateAsync): quản trị viên
        // cấp lại mật khẩu đúng lúc này thì một trong hai bên phải chờ bên kia, không có chuyện bên ghi
        // sau lặng lẽ đè mất mật khẩu bên kia vừa đặt.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var user = await _unitOfWork.Users.LockForUpdateAsync(userId, cancellationToken);

        var guard = GuardAccountState(user);
        if (guard.IsFailure)
        {
            return guard;
        }

        var account = user!;

        // Mật khẩu không trim: khoảng trắng đầu và cuối là ký tự hợp lệ, cắt đi là âm thầm đổi thứ
        // người dùng đã gõ.
        var currentPassword = request.CurrentPassword ?? string.Empty;
        var newPassword = request.NewPassword ?? string.Empty;

        if (newPassword.Length < ValidationPatterns.MinPasswordLength)
        {
            return FieldFailure(
                NewPasswordField,
                $"Mật khẩu mới phải có ít nhất {ValidationPatterns.MinPasswordLength} ký tự.");
        }

        if (string.Equals(newPassword, currentPassword, StringComparison.Ordinal))
        {
            return FieldFailure(NewPasswordField, "Mật khẩu mới phải khác mật khẩu hiện tại.");
        }

        // Xác minh thật, bằng chuỗi băm trong database. Người gọi đã qua [Authorize] và đang đổi mật
        // khẩu của chính mình nên thông điệp cụ thể ở đây không làm lộ gì.
        //
        // 400 gắn vào ô currentPassword, KHÔNG phải 401: gõ sai một ô là lỗi nhập liệu, phiên vẫn còn
        // nguyên. Frontend coi mọi 401 trên request có token là phiên đã chết — trả 401 ở đây thì
        // người dùng bị đăng xuất và trang đăng nhập đưa "mật khẩu hiện tại không đúng" ra làm lý do.
        if (!_passwordHasher.Verify(currentPassword, account.PasswordHash))
        {
            return FieldFailure(CurrentPasswordField, "Mật khẩu hiện tại không đúng.");
        }

        account.PasswordHash = _passwordHasher.Hash(newPassword);

        // Xoay SecurityStamp: mọi JWT đã cấp trước thời điểm này lập tức mất hiệu lực (§5) — kể cả
        // token người dùng đang cầm, nên client phải đăng nhập lại sau khi đổi mật khẩu. Đó là cái
        // giá phải trả để một mật khẩu bị lộ không sống thêm được cho tới lúc token hết hạn.
        account.SecurityStamp = NewSecurityStamp();
        account.MustChangePassword = false;
        account.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Sau commit, không phải trước: xem IUserConnectionCloser. Kết nối ở chính tab vừa đổi mật khẩu
        // cũng bị cắt — nó đang cầm token vừa chết, như mọi tab khác.
        _connections.CloseConnectionsOf(account.Id);

        return Result.Success();
    }

    /// <inheritdoc />
    public Task<Result> LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Task.FromResult(Result.Unauthorized(SessionExpiredMessage));
        }

        // Chưa có refresh token để thu hồi nên phía server không còn gì phải xoá; client bỏ access
        // token đi là xong. Cố ý KHÔNG xoay SecurityStamp: người dùng bấm đăng xuất ở một máy chứ
        // không yêu cầu đá mọi thiết bị khác của chính họ ra ngoài.
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Kiểm tra tài khoản còn dùng được không, cho bốn thao tác đã nằm sau <c>[Authorize]</c>.
    /// Token hợp lệ về chữ ký vẫn có thể trỏ tới một tài khoản đã bị khoá hoặc đã đổi mật khẩu sau
    /// khi token được cấp — hai trường hợp đó phải chặn ở đây (§5).
    /// </summary>
    private Result GuardAccountState(User? user)
    {
        if (user is null)
        {
            // Token còn hạn nhưng tài khoản không còn: 401 để client đăng nhập lại, không phải 404.
            return Result.Unauthorized(SessionExpiredMessage);
        }

        if (user.Status != UserStatus.Active)
        {
            return Result.Forbidden(MessageForBlockedStatus(user.Status));
        }

        // Claim vắng mặt thì không có gì để so — tầng Api chịu trách nhiệm luôn đặt claim
        // security_stamp vào token, đúng như IJwtTokenGenerator đã cam kết.
        var stamp = _currentUser.SecurityStamp;
        if (!string.IsNullOrEmpty(stamp) && !string.Equals(stamp, user.SecurityStamp, StringComparison.Ordinal))
        {
            return Result.Unauthorized(SessionExpiredMessage);
        }

        return Result.Success();
    }

    /// <summary>
    /// Dựng hồ sơ trả về. Tên nhóm và tên môn đọc từ navigation nếu chỗ gọi đã nạp kèm; chỉ khi thiếu
    /// mới truy vấn thêm, nên đường ghi (nạp entity trần) không phải trả giá bằng lazy loading.
    /// </summary>
    private async Task<UserProfileDto> BuildProfileDtoAsync(User user, CancellationToken cancellationToken)
    {
        var groupName = user.Group?.Name;
        if (groupName is null && user.GroupId is { } groupId)
        {
            groupName = (await _unitOfWork.StudentGroups.GetByIdAsync(groupId, cancellationToken))?.Name;
        }

        var subjectName = user.Subject?.Name;
        if (subjectName is null && user.SubjectId is { } subjectId)
        {
            subjectName = (await _unitOfWork.Subjects.GetByIdAsync(subjectId, cancellationToken))?.Name;
        }

        return user.ToProfileDto(groupName, subjectName);
    }

    /// <summary>
    /// Đối chiếu vai trò client khai báo với vai trò thật. Client không gửi gì thì không có gì để đối
    /// chiếu — hồ sơ trả về đã nói rõ vai trò thật nên frontend vẫn điều hướng đúng khu vực.
    /// </summary>
    private static bool MatchesRequestedRole(string? requestedRole, UserRole actualRole)
    {
        if (string.IsNullOrWhiteSpace(requestedRole))
        {
            return true;
        }

        // Giá trị lạ tính là "sai vai trò" chứ không phải "bỏ qua", đúng ý chú thích ở LoginRequest.Role.
        return UserMappingExtensions.TryParseRole(requestedRole, out var parsedRole) && parsedRole == actualRole;
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = _currentUser.UserId ?? 0;
        return _currentUser.IsAuthenticated && _currentUser.UserId.HasValue;
    }

    /// <summary>Chuỗi rỗng nghĩa là xoá trắng trường đó, nên quy về NULL trước khi lưu.</summary>
    private static string? NormalizeOptional(string? value)
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
    /// Lỗi 400 gắn vào đúng một ô của form, cùng hình dạng <c>errors</c> mà <c>ValidationFilter</c> trả,
    /// để frontend hiện thông báo ngay dưới ô đó thay vì một dòng chung phía trên form.
    /// </summary>
    private static Result FieldFailure(string field, string message) =>
        Result.Failure(
            message,
            ErrorCode.Validation,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] });

    /// <summary>
    /// Đối chiếu mật khẩu với một chuỗi băm giả để nhánh "không có tài khoản" tốn xấp xỉ cùng thời
    /// gian với nhánh "sai mật khẩu". Kết quả cố tình bỏ đi.
    /// </summary>
    /// <remarks>
    /// Chuỗi băm giả lấy từ chính <see cref="IPasswordHasher"/> nên nó luôn mang đúng work factor mà
    /// mật khẩu thật đang dùng. Không có nó, nhánh "tài khoản không tồn tại" trả lời sau vài mili giây
    /// còn nhánh "sai mật khẩu" sau vài trăm — dùng chung một thông điệp thì vẫn dò ra được tài khoản
    /// nào có thật, chỉ bằng đồng hồ bấm giây.
    /// </remarks>
    private void BurnPasswordVerificationTime(string password)
    {
        try
        {
            _ = _passwordHasher.Verify(password, _passwordHasher.TimingDecoyHash);
        }
        catch
        {
            // Không có tác dụng phụ nào cần cứu: đây thuần tuý là công đoạn tiêu thời gian.
        }
    }

    /// <summary>Câu chữ tương ứng với từng trạng thái không cho đăng nhập.</summary>
    /// <remarks>
    /// Người gọi tới được đây đã chứng minh biết mật khẩu, nên nói cụ thể không làm lộ tài khoản
    /// nào có thật nữa — và một câu chung chung sẽ khiến học sinh gọi lên hỏi mà tổng đài cũng
    /// không đoán được chuyện gì.
    /// <para>
    /// Câu chữ nằm ở <see cref="SessionValidation"/> vì phép kiểm phiên trên mọi endpoint cũng nói
    /// đúng những câu này.
    /// </para>
    /// </remarks>
    private static string MessageForBlockedStatus(UserStatus status) =>
        SessionValidation.BlockedStatusMessage(status);
}
