using Anthill.Application.Common;
using Anthill.Application.DTOs.Notifications;
using Anthill.Application.Interfaces.Realtime;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;

namespace Anthill.Application.Services;

/// <summary>
/// Cài đặt <see cref="INotificationService"/> — bốn endpoint của §4.2.
/// </summary>
/// <remarks>
/// Hai điều quyết định nằm ở file này:
/// <list type="number">
/// <item><b>Thông báo thuộc về một người, không thuộc về một vai trò.</b> Mock lọc theo vai trò
/// nên mọi giáo viên nhìn chung một dòng thông báo. Ở đây mỗi bản ghi có đúng một chủ, và "thông
/// báo của tôi" là phép lọc theo khoá ngoại — không có cách nào đọc nhầm sang người khác.</item>
/// <item><b>Số chưa đọc đổi thì bắn realtime ngay.</b> Endpoint <c>unread-count</c> vốn bị gọi lại
/// mỗi lần đổi trang ở cả hai layout, nên nó là endpoint bị gọi nhiều nhất hệ thống. Đẩy sự kiện
/// thay cho việc để client hỏi lại (§7).</item>
/// </list>
/// </remarks>
public sealed class NotificationService : INotificationService
{
    private const string NotFoundMessage = "Không tìm thấy thông báo này.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IRealtimeNotificationService _realtime;

    public NotificationService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IRealtimeNotificationService realtime)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _realtime = realtime;
    }

    private int CurrentUserId => _currentUser.RequireUserId();

    /// <inheritdoc />
    public async Task<Result<PagedResult<NotificationDto>>> ListAsync(
        ListNotificationsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = await _unitOfWork.Notifications.GetForUserPagedAsync(
            CurrentUserId,
            query.UnreadOnly,
            query,
            cancellationToken);

        return Result.Success(page.Map(notification => notification.ToDto()));
    }

    /// <inheritdoc />
    public async Task<Result<int>> GetUnreadCountAsync(CancellationToken cancellationToken = default) =>
        Result.Success(await _unitOfWork.Notifications.CountUnreadAsync(CurrentUserId, cancellationToken));

    /// <inheritdoc />
    public async Task<Result> MarkAsReadAsync(int id, CancellationToken cancellationToken = default)
    {
        // Truy vấn lọc sẵn theo chủ sở hữu, nên thông báo của người khác trả về NULL y như thông
        // báo không tồn tại — và cả hai cùng nhận 404. Phân biệt chúng là để lộ id nào có thật.
        var notification = await _unitOfWork.Notifications.GetOwnedAsync(id, CurrentUserId, cancellationToken);
        if (notification is null)
        {
            return Result.NotFound(NotFoundMessage);
        }

        // Đánh dấu lại một thông báo đã đọc không phải lỗi, chỉ là không có gì để làm. Trả lỗi ở
        // đây thì hai tab cùng mở là một tab nhận thông báo đỏ vô cớ.
        if (notification.IsRead)
        {
            return Result.Success();
        }

        notification.IsRead = true;
        notification.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Notifications.Update(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishUnreadCountAsync(cancellationToken);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<int>> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        var affected = await _unitOfWork.Notifications.MarkAllReadAsync(CurrentUserId, cancellationToken);

        if (affected == 0)
        {
            return Result.Success(0);
        }

        // Repository cố ý chỉ đánh dấu trên các thực thể đang được theo dõi và để việc ghi cho
        // UnitOfWork — ranh giới ghi của cả hệ thống nằm ở đây. Quên dòng này thì endpoint trả về
        // "đã đánh dấu N mục", số chưa đọc đọc lại từ SQL vẫn nguyên, và người dùng bấm mãi không
        // thấy chấm đỏ tắt.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishUnreadCountAsync(cancellationToken);

        return Result.Success(affected);
    }

    /// <summary>Đẩy số chưa đọc mới nhất cho chính người vừa thao tác.</summary>
    /// <remarks>
    /// Đọc lại từ database chứ không trừ dần trong bộ nhớ: người dùng có thể đang mở hai tab, và
    /// một con số tính từ trạng thái mà tab này nhìn thấy sẽ sai ngay khi tab kia vừa đọc một
    /// thông báo khác.
    ///
    /// Lỗi khi bắn <b>không</b> được làm hỏng thao tác chính — thông báo là thứ yếu, còn việc đánh
    /// dấu đã đọc thì đã ghi xong rồi. Phần nuốt lỗi nằm ở tầng cài đặt realtime.
    /// </remarks>
    private async Task PublishUnreadCountAsync(CancellationToken cancellationToken)
    {
        var unread = await _unitOfWork.Notifications.CountUnreadAsync(CurrentUserId, cancellationToken);
        await _realtime.UnreadCountChangedAsync(CurrentUserId, unread, cancellationToken);
    }
}
