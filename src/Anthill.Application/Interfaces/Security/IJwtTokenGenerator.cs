using Anthill.Domain.Entities;

namespace Anthill.Application.Interfaces.Security;

/// <summary>
/// Cấp access token. Claim tối thiểu theo §4.1: <c>sub</c> = userId, <c>role</c>, <c>name</c>,
/// <c>subject_id</c> (giáo viên) và <c>security_stamp</c>.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Sinh token cho tài khoản đã xác thực thành công. Mọi claim đọc từ entity
    /// <see cref="User"/> lấy trong database — không bao giờ từ request (§3 quy tắc ⑤).
    /// </summary>
    AccessTokenResult GenerateToken(User user);

    /// <summary>
    /// Chuỗi ngẫu nhiên mật mã học, không mang thông tin. Học sinh làm bài 45–60 phút nên
    /// access token hết hạn giữa chừng là mất bài — refresh token là đường cấp lại (§4.1).
    /// </summary>
    string GenerateRefreshToken();
}

/// <summary>Token đã ký kèm mốc hết hạn UTC, đúng hai trường frontend cần cho <c>LoginResponse</c>.</summary>
public sealed record AccessTokenResult
{
    public required string AccessToken { get; init; }

    /// <summary>UTC. Frontend dựa vào mốc này để chủ động gọi refresh trước khi hết hạn.</summary>
    public required DateTime ExpiresAt { get; init; }
}
