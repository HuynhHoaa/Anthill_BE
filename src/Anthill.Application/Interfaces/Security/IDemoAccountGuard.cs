using Anthill.Domain.Entities;

namespace Anthill.Application.Interfaces.Security;

/// <summary>
/// Nhận diện ba tài khoản demo dùng chung, công khai trên trang đăng nhập (xem <c>DbSeeder</c>).
/// </summary>
/// <remarks>
/// Ba tài khoản này là lối vào duy nhất để một người ghé thăm dùng thử hệ thống mà không cần tự
/// tạo tài khoản — bất kỳ ai cũng đăng nhập được bằng đúng thông tin in sẵn trên trang đăng nhập.
/// Không có gì chặn một người ghé thăm đổi mật khẩu, đổi email, tự khoá hay tự xoá chính tài khoản
/// đó, sau đó bản demo công khai hỏng cho tới khi có người vào database sửa lại tay. Interface này
/// tách khỏi service để danh sách bảo vệ đọc từ cấu hình (<c>DemoAccountProtectionOptions</c>),
/// không phải một danh sách id viết chết trong code — id tài khoản đổi theo từng lần seed database,
/// còn email/username thì không.
/// </remarks>
public interface IDemoAccountGuard
{
    /// <summary>True nếu <paramref name="user"/> là một trong các tài khoản demo cần bảo vệ.</summary>
    bool IsProtected(User user);
}
