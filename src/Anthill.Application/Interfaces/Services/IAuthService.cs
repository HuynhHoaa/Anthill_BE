using Anthill.Application.Common;
using Anthill.Application.DTOs.Auth;

namespace Anthill.Application.Interfaces.Services;

/// <summary>
/// Xác thực và hồ sơ cá nhân — sáu endpoint của §4.1. Đây là service duy nhất được cài đặt
/// đầy đủ ở giai đoạn này; không có nó thì không test được gì khác (§9).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// <c>POST /api/auth/login</c>. Ba điều bắt buộc:
    /// vai trò đọc từ database chứ không từ <c>request.Role</c> (§3 quy tắc ⑤);
    /// tài khoản <c>Locked</c> bị chặn với <see cref="ErrorCode.Forbidden"/> (§5);
    /// sai tài khoản và sai mật khẩu trả <b>cùng một</b> thông điệp để không dò được tài khoản nào có thật.
    /// </summary>
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /api/auth/refresh</c>. Cấp lại access token; phải kiểm lại trạng thái tài khoản
    /// và <c>SecurityStamp</c> tại thời điểm gọi, vì tài khoản có thể đã bị khoá sau khi token được cấp.
    /// </summary>
    Task<Result<LoginResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/auth/me</c>. Danh tính lấy từ <c>ICurrentUser</c>, không nhận id từ client (§3 quy tắc ④).</summary>
    Task<Result<UserProfileDto>> GetCurrentProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /api/auth/me</c>. Trim mọi chuỗi trước khi lưu và trước khi so trùng email,
    /// nếu không <c>'a@b.c '</c> và <c>'a@b.c'</c> cùng lọt qua unique index (§5).
    /// Email trùng người khác trả <see cref="ErrorCode.Conflict"/>.
    /// </summary>
    Task<Result<UserProfileDto>> UpdateCurrentProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /api/auth/me/avatar</c>. Đặt hay gỡ ảnh đại diện của chính mình; chỉ quản trị viên và giáo viên
    /// (<see cref="AvatarPolicy"/>), học sinh nhận <see cref="ErrorCode.Forbidden"/>.
    /// </summary>
    Task<Result<UserProfileDto>> UpdateCurrentAvatarAsync(
        UpdateAvatarRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /api/auth/change-password</c>. Đổi mật khẩu thành công phải sinh
    /// <c>SecurityStamp</c> mới và tắt cờ <c>MustChangePassword</c>.
    /// Sai mật khẩu hiện tại là <see cref="ErrorCode.Validation"/> kèm lỗi của trường
    /// <c>currentPassword</c>, không phải <see cref="ErrorCode.Unauthorized"/>: phiên vẫn còn sống.
    /// </summary>
    Task<Result> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /api/auth/logout</c>. Thu hồi phiên hiện tại. Với JWT thuần thì việc thật sự
    /// diễn ra là huỷ refresh token; access token còn hạn chỉ chết khi <c>SecurityStamp</c> đổi.
    /// </summary>
    Task<Result> LogoutAsync(CancellationToken cancellationToken = default);
}
