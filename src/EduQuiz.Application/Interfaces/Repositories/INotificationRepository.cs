using EduQuiz.Application.Common;
using EduQuiz.Domain.Entities;

namespace EduQuiz.Application.Interfaces.Repositories;

/// <summary>
/// Thông báo luôn gắn với <c>RecipientUserId</c> cụ thể, không phát theo vai trò như mock.
/// Mọi hàm ở đây đều nhận <c>userId</c> — không có đường nào đọc thông báo của người khác (§4.2).
/// </summary>
public interface INotificationRepository : IGenericRepository<Notification>
{
    /// <summary>Mới nhất trước.</summary>
    Task<PagedResult<Notification>> GetForUserPagedAsync(
        int userId,
        bool unreadOnly,
        PaginationParams pagination,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trả NULL khi thông báo không tồn tại <b>hoặc</b> không thuộc người gọi. Service trả 404
    /// cho cả hai trường hợp để không ai dò được id hợp lệ (§4.2).
    /// </summary>
    Task<Notification?> GetOwnedAsync(int notificationId, int userId, CancellationToken cancellationToken = default);

    /// <summary>Đánh dấu tất cả đã đọc, trả về số bản ghi vừa đổi.</summary>
    Task<int> MarkAllReadAsync(int userId, CancellationToken cancellationToken = default);
}
