using Anthill.Application.Common;
using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Entities;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

/// <summary>
/// Mọi hàm ở đây đều nhận <c>userId</c> và đưa nó vào mệnh đề <c>WHERE</c>: không có đường nào
/// đọc hay sửa thông báo của người khác (§4.2).
/// </summary>
public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
{
    public NotificationRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<Notification>> GetForUserPagedAsync(
        int userId,
        bool unreadOnly,
        PaginationParams pagination,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Where(n => n.RecipientUserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .ToPagedResultAsync(pagination, cancellationToken);
    }

    public async Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken = default) =>
        await DbSet.CountAsync(n => n.RecipientUserId == userId && !n.IsRead, cancellationToken);

    /// <summary>
    /// Điều kiện quyền sở hữu nằm ngay trong truy vấn, nên "không tồn tại" và "của người khác"
    /// trả về cùng một thứ: NULL. Service dịch cả hai thành 404 để không ai dò được id hợp lệ.
    /// </summary>
    public async Task<Notification?> GetOwnedAsync(
        int notificationId,
        int userId,
        CancellationToken cancellationToken = default) =>
        await DbSet.FirstOrDefaultAsync(
            n => n.Id == notificationId && n.RecipientUserId == userId,
            cancellationToken);

    public async Task<int> MarkAllReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        // Cố ý không dùng ExecuteUpdate: nó ghi ngay lập tức, còn ranh giới ghi của hệ thống này
        // là IUnitOfWork.SaveChangesAsync. Số thông báo chưa đọc của một người luôn nhỏ.
        var unread = await DbSet
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }

        return unread.Count;
    }

    /// <summary>
    /// Hai bước: trước hết tìm id các lượt làm bài của <paramref name="studentId"/> (nối qua
    /// <c>ExamAttempt.Assignment.StudentId</c> — không có navigation thẳng từ <c>Notification</c> sang
    /// <c>ExamAttempt</c> vì <c>TargetType</c>/<c>TargetId</c> cố ý là tham chiếu đa hình, không phải
    /// khoá ngoại thật), rồi lọc thông báo theo đúng những id đó.
    /// </summary>
    public async Task<List<Notification>> GetByAttemptOwnerAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var attemptIds = await Context.ExamAttempts
            .Where(a => a.Assignment.StudentId == studentId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (attemptIds.Count == 0)
        {
            return new List<Notification>();
        }

        return await DbSet
            .Where(n => n.TargetType == nameof(ExamAttempt) && n.TargetId != null && attemptIds.Contains(n.TargetId.Value))
            .ToListAsync(cancellationToken);
    }
}
