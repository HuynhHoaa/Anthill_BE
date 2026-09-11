using Anthill.Application.Common;
using Anthill.Application.DTOs.Notifications;

namespace Anthill.Application.Interfaces.Services;

/// <summary>Thông báo của chính người đang đăng nhập — §4.2.</summary>
/// <remarks>
/// Ràng buộc đã cài:
/// <list type="bullet">
/// <item>Thông báo gắn với <c>RecipientUserId</c> cụ thể, <b>không</b> lọc theo vai trò như mock.</item>
/// <item><see cref="MarkAsReadAsync"/> trả <see cref="ErrorCode.NotFound"/> — 404 chứ không phải
/// 403 — khi id không thuộc người gọi. Mock tìm theo id trên toàn mảng, tức bất kỳ ai cũng đọc
/// được thông báo của người khác.</item>
/// <item>Mọi thay đổi số chưa đọc kéo theo sự kiện <c>UnreadCountChanged</c>: đó là thứ thay thế
/// cho việc poll <c>unread-count</c> mỗi lần đổi trang.</item>
/// <item>Client không được phép tạo thông báo — không có endpoint nào cho việc đó.</item>
/// </list>
/// </remarks>
public interface INotificationService
{
    Task<Result<PagedResult<NotificationDto>>> ListAsync(
        ListNotificationsQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<int>> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    Task<Result> MarkAsReadAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Trả về số thông báo vừa được đánh dấu, để giao diện nói được "đã đánh dấu N mục".</summary>
    Task<Result<int>> MarkAllAsReadAsync(CancellationToken cancellationToken = default);
}
