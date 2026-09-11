namespace Anthill.Application.DTOs.Auth;

/// <summary>Kết quả đăng nhập, đúng hình dạng §4.1.</summary>
public sealed record LoginResponse
{
    public required string AccessToken { get; init; }

    /// <summary>Luôn là <c>"Bearer"</c>; có mặt để client dựng header mà không phải nối chuỗi cứng.</summary>
    public string TokenType { get; init; } = "Bearer";

    public required DateTime ExpiresAt { get; init; }

    /// <summary>NULL khi hệ thống chưa bật refresh token.</summary>
    public string? RefreshToken { get; init; }

    public required UserProfileDto User { get; init; }

    /// <summary>
    /// Bật khi mật khẩu hiện tại do quản trị viên cấp hoặc vừa reset. §5 yêu cầu ép đổi ở lần
    /// đăng nhập đầu, mà chỗ duy nhất nói được điều đó cho client là ngay tại response đăng nhập.
    /// </summary>
    public bool MustChangePassword { get; init; }
}
