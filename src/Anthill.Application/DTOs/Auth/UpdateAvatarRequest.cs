namespace Anthill.Application.DTOs.Auth;

/// <summary>
/// Thân request của <c>PUT /api/auth/me/avatar</c> — đặt hay gỡ ảnh đại diện của <b>chính mình</b>.
/// </summary>
/// <remarks>
/// Ảnh tải lên trước ở <c>POST /api/uploads/images</c>, rồi đường dẫn nhận về mới được gửi vào đây — cùng luồng với ảnh
/// câu hỏi. Gỡ ảnh bằng <c>null</c> hay chuỗi rỗng, trở về chữ cái đầu. Trường phải có mặt trong JSON: thiếu nó là 400,
/// để một client quên gửi không âm thầm gỡ mất ảnh của người dùng.
/// </remarks>
public sealed record UpdateAvatarRequest
{
    public required string? AvatarUrl { get; init; }
}
