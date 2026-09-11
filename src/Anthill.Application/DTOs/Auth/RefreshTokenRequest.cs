namespace Anthill.Application.DTOs.Auth;

/// <summary>
/// Thân request của <c>POST /api/auth/refresh</c>. Cần thiết vì học sinh làm bài 45–60 phút,
/// access token hết hạn giữa chừng là mất bài (§4.1).
/// </summary>
public sealed record RefreshTokenRequest
{
    public required string RefreshToken { get; init; }
}
