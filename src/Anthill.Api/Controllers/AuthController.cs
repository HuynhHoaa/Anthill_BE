using Anthill.Api.Security;
using Anthill.Application.Common;
using Anthill.Application.DTOs.Auth;
using Anthill.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Anthill.Api.Controllers;

/// <summary>
/// Sáu endpoint xác thực và hồ sơ cá nhân của §4.1.
/// </summary>
/// <remarks>
/// Controller này <b>không chứa logic nghiệp vụ</b>: mỗi action gọi đúng một phương thức của
/// <see cref="IAuthService"/> rồi để <see cref="ApiControllerBase.FromResult{TValue}"/> dịch kết quả
/// sang HTTP. Không endpoint nào nhận <c>userId</c> từ client — danh tính suy từ JWT qua
/// <c>ICurrentUser</c> (§3 quy tắc ④).
/// </remarks>
[Authorize]
[Route("api/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Đăng nhập bằng email (giáo viên, quản trị viên) hoặc username / mã học sinh.</summary>
    /// <remarks>
    /// Endpoint duy nhất không cần token (§1). Trường <c>role</c> trong body chỉ để đối chiếu;
    /// vai trò thật luôn đọc từ database (§3 quy tắc ⑤).
    /// </remarks>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _authService.LoginAsync(request, cancellationToken));

    /// <summary>Cấp lại access token từ refresh token.</summary>
    /// <remarks>
    /// Chưa dùng được: hệ thống chưa có bảng lưu refresh token nên <c>LoginAsync</c> không phát hành
    /// chuỗi nào, và cấp access token dựa trên một chuỗi server không kiểm chứng được là tự mở cửa
    /// sau. Service trả 401 để client đăng nhập lại — đúng thứ frontend đã biết cách xử lý; endpoint
    /// vẫn đứng sẵn ở đây để bước sau chỉ phải thay phần ruột.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _authService.RefreshAsync(request, cancellationToken));

    /// <summary>Hồ sơ của người đang đăng nhập, suy từ JWT.</summary>
    /// <remarks>
    /// Token của tài khoản bị khoá hay chờ xoá nhận <b>403</b> kèm lý do ở đây, trong khi mọi endpoint
    /// nghiệp vụ trả 401 — xem <see cref="ReportsBlockedAccountAttribute"/>. Còn dùng được khi tài
    /// khoản đang phải đổi mật khẩu (§5): frontend cần hồ sơ này để dựng màn hình bắt buộc đổi mật
    /// khẩu — xem <see cref="AllowWhileMustChangePasswordAttribute"/>.
    /// </remarks>
    [ReportsBlockedAccount]
    [AllowWhileMustChangePassword]
    [HttpGet("me")]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
        => FromResult(await _authService.GetCurrentProfileAsync(cancellationToken));

    /// <summary>Cập nhật thông tin liên hệ của chính mình.</summary>
    /// <remarks>Email trùng tài khoản khác trả 409, không phải 400 (§5).</remarks>
    [ReportsBlockedAccount]
    [HttpPut("me")]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMe(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _authService.UpdateCurrentProfileAsync(request, cancellationToken));

    /// <summary>Đặt hay gỡ ảnh đại diện của chính mình.</summary>
    /// <remarks>
    /// Ảnh tải lên trước ở <c>POST /api/uploads/images</c>, rồi gửi đường dẫn nhận về vào đây; bỏ trống là gỡ ảnh.
    /// Chỉ quản trị viên và giáo viên — học sinh nhận 403.
    /// </remarks>
    [ReportsBlockedAccount]
    [HttpPut("me/avatar")]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateMyAvatar(
        [FromBody] UpdateAvatarRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _authService.UpdateCurrentAvatarAsync(request, cancellationToken));

    /// <summary>Đổi mật khẩu, có xác minh mật khẩu hiện tại.</summary>
    /// <remarks>
    /// Đổi thành công sẽ xoay <c>SecurityStamp</c>, nên <b>mọi</b> token đã cấp trước đó mất hiệu lực
    /// ngay — kể cả token client đang cầm. Frontend phải điều hướng về màn hình đăng nhập sau khi
    /// nhận 200 ở đây.
    /// <para>
    /// Sai mật khẩu hiện tại là <b>400</b> kèm <c>errors.currentPassword</c>. 401 chỉ dành cho phiên đã
    /// chết, vì frontend đăng xuất người dùng ngay khi gặp nó.
    /// </para>
    /// <para>
    /// Đây là lối thoát duy nhất khi tài khoản đang phải đổi mật khẩu (§5), nên luôn gọi được bất kể
    /// cờ đó — xem <see cref="AllowWhileMustChangePasswordAttribute"/>.
    /// </para>
    /// </remarks>
    [ReportsBlockedAccount]
    [EnableRateLimiting("auth")]
    [AllowWhileMustChangePassword]
    [HttpPost("change-password")]
    [ProducesResponseType<ApiMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
        => FromResult(
            await _authService.ChangePasswordAsync(request, cancellationToken),
            "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.");

    /// <summary>Thu hồi phiên hiện tại.</summary>
    /// <remarks>
    /// Tài khoản đang phải đổi mật khẩu (§5) vẫn đăng xuất được — xem
    /// <see cref="AllowWhileMustChangePasswordAttribute"/>.
    /// </remarks>
    [AllowWhileMustChangePassword]
    [HttpPost("logout")]
    [ProducesResponseType<ApiMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        => FromResult(await _authService.LogoutAsync(cancellationToken), "Đã đăng xuất.");
}
