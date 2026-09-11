namespace Anthill.Application.DTOs.Auth;

/// <summary>
/// Thân request của <c>POST /api/auth/login</c> — endpoint duy nhất không cần token (§1).
/// </summary>
public sealed record LoginRequest
{
    /// <summary>
    /// Chuỗi người dùng gõ ở ô đăng nhập: email với giáo viên và quản trị viên,
    /// username hoặc mã học sinh với học sinh. Server tự dò cả ba, đã trim.
    /// </summary>
    public required string Username { get; init; }

    public required string Password { get; init; }

    /// <summary>
    /// Vai trò client chọn trên giao diện. <b>Chỉ để đối chiếu</b> — vai trò thật luôn đọc từ
    /// cột <c>Role</c> trong database. §3 quy tắc ⑤: tin trường này là leo thang quyền tức thì,
    /// gửi <c>role: 'admin'</c> là thành quản trị viên.
    /// Kiểu <c>string</c> chứ không phải enum để một giá trị lạ trở thành "sai vai trò"
    /// thay vì làm hỏng model binding.
    /// </summary>
    public string? Role { get; init; }
}
