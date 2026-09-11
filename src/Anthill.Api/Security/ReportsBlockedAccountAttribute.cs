namespace Anthill.Api.Security;

/// <summary>
/// Đánh dấu endpoint tự trả lời người có tài khoản bị khoá, chờ xoá hoặc đã xoá — bằng 403 kèm lý do —
/// nên phép kiểm phiên ở <c>OnTokenValidated</c> để các trường hợp đó đi tiếp tới service.
/// </summary>
/// <remarks>
/// <para>
/// Mọi endpoint khác trả <b>401</b> cho token của tài khoản bị chặn, vì 401 là tín hiệu duy nhất khiến
/// frontend đưa người dùng về trang đăng nhập; 403 ở đó chỉ hiện một thông báo "không có quyền" trong
/// khi em vẫn ngồi trong giao diện. Còn <c>GET /api/auth/me</c> là nơi client hỏi "vì sao tôi không
/// vào được", nên nó giữ hợp đồng cũ: 403 kèm câu nói rõ phải liên hệ ai.
/// </para>
/// <para>
/// Chỉ nới cho ba trạng thái tài khoản. Token hỏng, tài khoản không còn, hay stamp đã xoay trên một
/// tài khoản đang hoạt động vẫn bị chặn ngay ở tầng xác thực. Endpoint gắn thuộc tính này
/// <b>bắt buộc</b> phải tự kiểm trạng thái tài khoản — hiện là ba endpoint gọi
/// <c>AuthService.GuardAccountState</c>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ReportsBlockedAccountAttribute : Attribute;
