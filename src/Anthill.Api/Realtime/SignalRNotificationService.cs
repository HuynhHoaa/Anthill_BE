using Anthill.Api.Hubs;
using Anthill.Application.Interfaces.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Anthill.Api.Realtime;

/// <summary>
/// Cài đặt <see cref="IRealtimeNotificationService"/> bằng SignalR — sáu sự kiện của §7.
/// </summary>
/// <remarks>
/// <para>
/// <b>Vì sao nằm ở Api chứ không ở Infrastructure</b> như ghi chú ban đầu trên interface: hub là
/// một thành phần của ASP.NET Core và <c>IHubContext&lt;NotificationHub&gt;</c> chỉ có nghĩa ở nơi
/// hub được đăng ký. Đưa xuống Infrastructure thì phải kéo cả gói SignalR và kiểu hub xuống theo —
/// tức là tầng hạ tầng dữ liệu bắt đầu biết về giao thức của tầng web. Tầng Application vẫn không
/// biết gì, vì nó chỉ nhìn thấy interface.
/// </para>
/// <para>
/// <b>Mọi lời gọi ở đây đều nuốt lỗi.</b> Thông báo realtime là thứ yếu: bản ghi trong database đã
/// được ghi trong transaction nghiệp vụ rồi, và người dùng tải lại trang là thấy. Để một lỗi
/// SignalR ném ngược lên nghĩa là một học sinh nộp bài xong nhận về lỗi 500 chỉ vì đường truyền
/// tới giáo viên có trục trặc — bài đã chấm, điểm đã lưu, mà màn hình báo hỏng.
/// </para>
/// </remarks>
public sealed class SignalRNotificationService : IRealtimeNotificationService
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<NotificationHub> hub,
        ILogger<SignalRNotificationService> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task ExamAssignedAsync(
        IEnumerable<int> studentUserIds,
        ExamAssignedEvent payload,
        CancellationToken cancellationToken = default) =>
        SendToUsersAsync(studentUserIds, nameof(ExamAssignedEvent), "ExamAssigned", payload, cancellationToken);

    /// <inheritdoc />
    public Task ExamSubmittedAsync(
        int teacherUserId,
        ExamSubmittedEvent payload,
        CancellationToken cancellationToken = default) =>
        SafeAsync("ExamSubmitted", async () =>
        {
            // Hai đích cho cùng một sự kiện: giáo viên nhận ở mọi màn hình, còn group của đề dành
            // cho màn hình theo dõi đang mở — nơi cần con số submittedCount tự tăng mà không phải
            // hỏi lại server (§7).
            await _hub.Clients.Group(NotificationHub.UserGroup(teacherUserId))
                .SendAsync("ExamSubmitted", payload, cancellationToken);

            await _hub.Clients.Group(NotificationHub.ExamGroup(payload.ExamId))
                .SendAsync("ExamSubmitted", payload, cancellationToken);
        });

    /// <inheritdoc />
    public Task AutoSubmittedAsync(
        int studentUserId,
        AutoSubmittedStudentEvent studentPayload,
        int teacherUserId,
        AutoSubmittedTeacherEvent teacherPayload,
        CancellationToken cancellationToken = default) =>
        SafeAsync("AutoSubmitted", async () =>
        {
            // Hai payload khác nhau cho hai vai trò (§7): học sinh nhận thông báo về bài của chính
            // mình, giáo viên nhận thêm tên học sinh và số bài đã nộp. Gửi chung một payload thì
            // hoặc giáo viên thiếu thông tin, hoặc học sinh nhận kèm dữ liệu của người khác.
            await _hub.Clients.Group(NotificationHub.UserGroup(studentUserId))
                .SendAsync("AutoSubmitted", studentPayload, cancellationToken);

            await _hub.Clients.Group(NotificationHub.UserGroup(teacherUserId))
                .SendAsync("AutoSubmitted", teacherPayload, cancellationToken);

            await _hub.Clients.Group(NotificationHub.ExamGroup(teacherPayload.ExamId))
                .SendAsync("AutoSubmitted", teacherPayload, cancellationToken);
        });

    /// <inheritdoc />
    public Task DeadlineSoonAsync(
        int studentUserId,
        int teacherUserId,
        DeadlineSoonEvent payload,
        CancellationToken cancellationToken = default) =>
        SafeAsync("DeadlineSoon", async () =>
        {
            await _hub.Clients.Group(NotificationHub.UserGroup(studentUserId))
                .SendAsync("DeadlineSoon", payload, cancellationToken);

            await _hub.Clients.Group(NotificationHub.UserGroup(teacherUserId))
                .SendAsync("DeadlineSoon", payload, cancellationToken);
        });

    /// <inheritdoc />
    public Task ExamUpdatedAsync(
        int studentUserId,
        ExamUpdatedEvent payload,
        CancellationToken cancellationToken = default) =>
        SendToUsersAsync([studentUserId], nameof(ExamUpdatedEvent), "ExamUpdated", payload, cancellationToken);

    /// <inheritdoc />
    public Task UnreadCountChangedAsync(
        int userId,
        int unreadCount,
        CancellationToken cancellationToken = default) =>
        SafeAsync("UnreadCountChanged", () =>
            _hub.Clients.Group(NotificationHub.UserGroup(userId))
                .SendAsync("UnreadCountChanged", new { unreadCount }, cancellationToken));

    /// <summary>Gửi cùng một payload tới nhiều người dùng, mỗi người một group riêng.</summary>
    /// <remarks>
    /// Gửi theo group của từng người thay vì <c>Clients.Users(...)</c> để dùng đúng một cơ chế
    /// định danh trong cả file — hub tự cho mỗi kết nối vào group riêng lúc kết nối.
    /// </remarks>
    private Task SendToUsersAsync<TPayload>(
        IEnumerable<int> userIds,
        string payloadName,
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken) =>
        SafeAsync(payloadName, async () =>
        {
            foreach (var userId in userIds.Distinct())
            {
                await _hub.Clients.Group(NotificationHub.UserGroup(userId))
                    .SendAsync(eventName, payload, cancellationToken);
            }
        });

    /// <summary>Chạy một lần bắn và nuốt mọi lỗi, chỉ ghi log.</summary>
    /// <remarks>
    /// Bắt <see cref="Exception"/> trần là có chủ đích và hiếm khi đúng ở nơi khác. Ở đây thì đúng:
    /// không có loại lỗi nào từ tầng truyền tin đáng để làm hỏng một giao dịch nghiệp vụ đã commit.
    /// </remarks>
    private async Task SafeAsync(string eventName, Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Không gửi được sự kiện realtime {EventName}.", eventName);
        }
    }
}
