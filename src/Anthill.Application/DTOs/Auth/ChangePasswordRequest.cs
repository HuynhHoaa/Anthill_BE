namespace Anthill.Application.DTOs.Auth;

/// <summary>
/// Thân request của <c>POST /api/auth/change-password</c>. Server phải xác minh
/// <see cref="CurrentPassword"/> rồi mới đổi, và đổi luôn <c>SecurityStamp</c> để mọi JWT
/// còn hạn của tài khoản mất hiệu lực (§5).
/// </summary>
public sealed record ChangePasswordRequest
{
    public required string CurrentPassword { get; init; }

    public required string NewPassword { get; init; }
}
