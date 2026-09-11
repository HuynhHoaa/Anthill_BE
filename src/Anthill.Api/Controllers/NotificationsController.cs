using Anthill.Application.Common;
using Anthill.Application.DTOs.Notifications;
using Anthill.Application.Interfaces.Services;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anthill.Api.Controllers;

/// <summary>Thông báo của chính người đang đăng nhập — §4.2.</summary>
/// <remarks>
/// <para>
/// Mở cho cả ba vai trò, vì ai cũng nhận thông báo. Phạm vi không nằm ở vai trò mà ở
/// <c>RecipientUserId</c>: mỗi bản ghi thuộc về đúng một người, nên không có tham số nào để trỏ
/// sang hộp thư của người khác.
/// </para>
/// <para>
/// <b>Không có endpoint tạo thông báo.</b> Sinh thông báo là việc của backend, trong cùng
/// transaction với hành động nghiệp vụ sinh ra nó (§7). Frontend hiện tự thêm vào mảng mock —
/// đó là thứ không được mang lên server.
/// </para>
/// </remarks>
[Authorize(Roles = SystemRoles.All)]
[Route("api/notifications")]
public sealed class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>Thông báo của chính mình, mới nhất trước.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<NotificationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] ListNotificationsQuery query,
        CancellationToken cancellationToken)
        => FromResult(await _notificationService.ListAsync(query, cancellationToken));

    /// <summary>Số thông báo chưa đọc.</summary>
    /// <remarks>
    /// Endpoint này vốn bị gọi lại mỗi lần đổi trang ở cả hai layout nên là endpoint bị gọi nhiều
    /// nhất hệ thống. Sự kiện <c>UnreadCountChanged</c> qua SignalR là thứ thay thế nó — giữ lại
    /// đây cho lần nạp đầu tiên và cho trường hợp kết nối realtime chưa lên (§7).
    /// </remarks>
    [HttpGet("unread-count")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
        => FromResult(await _notificationService.GetUnreadCountAsync(cancellationToken));

    /// <summary>Đánh dấu một thông báo là đã đọc.</summary>
    /// <remarks>
    /// Thông báo của người khác trả <b>404</b>, không phải 403 — mock tìm theo id trên toàn mảng,
    /// tức bất kỳ ai cũng đọc được thông báo của người khác. Đánh dấu lại một mục đã đọc không
    /// phải lỗi: hai tab cùng mở là chuyện thường.
    /// </remarks>
    [HttpPatch("{id:int}/read")]
    [ProducesResponseType<ApiMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken)
        => FromResult(await _notificationService.MarkAsReadAsync(id, cancellationToken), "Đã đánh dấu đã đọc.");

    /// <summary>Đánh dấu toàn bộ thông báo của mình là đã đọc.</summary>
    /// <remarks>Trả về số mục vừa đánh dấu để giao diện nói được "đã đánh dấu N mục" thay vì một câu chung.</remarks>
    [HttpPost("mark-all-read")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
        => FromResult(await _notificationService.MarkAllAsReadAsync(cancellationToken));
}
