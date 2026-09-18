namespace Anthill.Api.Security;

/// <summary>
/// Đánh dấu endpoint vẫn gọi được khi tài khoản đang mang <c>MustChangePassword = true</c>.
/// </summary>
/// <remarks>
/// <para>
/// Mật khẩu do quản trị viên cấp (tạo tài khoản, cấp lại mật khẩu — §5) chỉ nên dùng được để đổi
/// mật khẩu, không phải để làm bài hay đọc dữ liệu. <c>MustChangePasswordMiddleware</c> chặn mọi
/// endpoint đã xác thực bằng 403 trong lúc cờ này còn bật, <b>trừ</b> những endpoint gắn thuộc tính
/// này — hiện là ba: <c>GET /api/auth/me</c> (frontend cần hồ sơ để dựng màn hình bắt buộc đổi mật
/// khẩu), <c>POST /api/auth/change-password</c> (chính lối thoát khỏi trạng thái này), và
/// <c>POST /api/auth/logout</c> (một tài khoản không đổi mật khẩu vẫn phải đăng xuất được).
/// </para>
/// <para>
/// Đây là lớp phòng thủ thứ hai: frontend đã có route guard đưa thẳng người dùng tới màn hình đổi
/// mật khẩu, nhưng một request gọi thẳng API (bỏ qua giao diện) không đi qua guard đó.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AllowWhileMustChangePasswordAttribute : Attribute;
