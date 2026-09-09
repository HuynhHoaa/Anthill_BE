namespace EduQuiz.Application.DTOs.Students;

/// <summary>
/// Thân request của <c>POST /api/students</c> — chỉ quản trị viên gọi được (§4.3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Vì sao <see cref="Password"/> bắt buộc.</b> Tài khoản học sinh do quản trị viên cấp, nên
/// người tạo phải cầm được mật khẩu để đưa cho học sinh. Hai cách khác đều tệ hơn: để server tự
/// sinh rồi trả về trong <c>StudentDto</c> là nhét một bí mật vào cái DTO được trả ở mọi màn hình
/// danh sách; còn sinh mà không trả về thì vừa tạo xong đã phải gọi ngay reset-password. Trường
/// hợp "server tự sinh" đã có endpoint riêng của nó là <c>POST /api/students/{id}/reset-password</c>,
/// với response riêng chỉ mang đúng mật khẩu.
/// </para>
/// <para>
/// Mật khẩu tạo ở đây luôn kèm cờ <c>MustChangePassword</c> (§5), nên nó chỉ sống tới lần đăng
/// nhập đầu tiên.
/// </para>
/// <para>
/// Cố ý không có <c>Phone</c>, <c>School</c> hay <c>AvatarColor</c>: đó là thông tin học sinh tự
/// điền qua <c>PUT /api/auth/me</c>, không phải thứ quản trị viên khai hộ lúc mở tài khoản.
/// Cũng không có <c>Role</c> — endpoint này chỉ tạo được học sinh, và <c>CreatedAt</c> do server
/// đặt bằng <c>DateTime.UtcNow</c> (§5).
/// </para>
/// </remarks>
public sealed record CreateStudentRequest
{
    public required string FullName { get; init; }

    public required string Email { get; init; }

    public required string Username { get; init; }

    /// <summary>Mã học sinh. Duy nhất toàn hệ thống, so trùng sau khi trim và bỏ hoa thường (§5).</summary>
    public required string Code { get; init; }

    /// <summary>Mật khẩu ban đầu. Học sinh bị ép đổi ở lần đăng nhập đầu tiên.</summary>
    public required string Password { get; init; }

    /// <summary>Bỏ trống hoặc 0 nghĩa là chưa xếp nhóm — tài khoản vẫn tạo được bình thường.</summary>
    public int? GroupId { get; init; }

    /// <summary><c>"active"</c> hoặc <c>"locked"</c>. Bỏ trống thì mặc định <c>"active"</c>.</summary>
    public string? Status { get; init; }
}
