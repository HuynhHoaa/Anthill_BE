namespace Anthill.Application.DTOs.Students;

/// <summary>
/// Kết quả của <c>POST /api/students/{id}/reset-password</c>.
/// </summary>
/// <remarks>
/// <b>Đây là lần duy nhất mật khẩu này rời server dưới dạng đọc được.</b> Database chỉ giữ chuỗi
/// băm BCrypt, nên quản trị viên đóng cửa sổ mà chưa kịp chép thì cách duy nhất là reset lại.
/// Đó là hành vi đúng chứ không phải thiếu sót: lưu thêm bản đọc được ở đâu đó để "xem lại sau"
/// nghĩa là một lần rò rỉ database sẽ lộ mật khẩu thật của toàn bộ học sinh.
/// </remarks>
public sealed record ResetPasswordResponse
{
    public required int StudentId { get; init; }

    /// <summary>Mật khẩu tạm, sinh bằng nguồn ngẫu nhiên mật mã. Chỉ sống tới lần đăng nhập đầu tiên.</summary>
    public required string Password { get; init; }

    /// <summary>Luôn là true — §5 bắt buộc ép đổi mật khẩu do quản trị viên cấp.</summary>
    public required bool MustChangePassword { get; init; }
}
