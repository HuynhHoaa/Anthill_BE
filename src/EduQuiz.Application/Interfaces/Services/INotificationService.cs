namespace EduQuiz.Application.Interfaces.Services;

/// <summary>Thông báo của chính người đang đăng nhập — §4.2.</summary>
/// <remarks>
/// Không có trong danh sách service của <c>EduQuiz_BE_Tech_Structure.md</c>, nhưng §4.2 có bốn
/// endpoint thật và <c>INotificationRepository</c> đã tồn tại — đặt sẵn interface ở đây để giai
/// đoạn Notification có chỗ điền thay vì phải nhớ ra là còn thiếu.
/// <code>
/// GET   /api/notifications               → Task&lt;Result&lt;PagedResult&lt;NotificationDto&gt;&gt;&gt; ListAsync(bool unreadOnly, PaginationParams pagination, CancellationToken ct)
/// GET   /api/notifications/unread-count  → Task&lt;Result&lt;int&gt;&gt; GetUnreadCountAsync(CancellationToken ct)
/// PATCH /api/notifications/{id}/read     → Task&lt;Result&gt; MarkAsReadAsync(int id, CancellationToken ct)
/// POST  /api/notifications/mark-all-read → Task&lt;Result&gt; MarkAllAsReadAsync(CancellationToken ct)
/// </code>
/// Ràng buộc phải cài (§4.2, §7):
/// <list type="bullet">
/// <item>Thông báo gắn với <c>RecipientUserId</c> cụ thể, <b>không</b> lọc theo vai trò như mock.</item>
/// <item><c>MarkAsReadAsync</c> trả <see cref="Common.ErrorCode.NotFound"/> — 404 chứ không phải
/// 403 — khi id không thuộc người gọi, để không ai dò được id hợp lệ. Mock hiện tìm theo id trên
/// toàn mảng, tức bất kỳ ai cũng đọc được thông báo của người khác.</item>
/// <item>Mọi thay đổi số chưa đọc phải kéo theo sự kiện <c>UnreadCountChanged</c>: đó là thứ thay
/// thế cho việc poll <c>unread-count</c> mỗi lần đổi trang.</item>
/// <item>Client không được phép tạo thông báo. Sinh thông báo là việc của backend, trong cùng
/// transaction với hành động nghiệp vụ.</item>
/// </list>
/// </remarks>
public interface INotificationService
{
}
