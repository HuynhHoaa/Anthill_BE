using Anthill.Application.Common;

namespace Anthill.Application.DTOs.Notifications;

/// <summary>Tham số của <c>GET /api/notifications</c>.</summary>
public sealed class ListNotificationsQuery : PaginationParams
{
    /// <summary>Chỉ lấy thông báo chưa đọc. Mặc định lấy tất cả.</summary>
    public bool UnreadOnly { get; set; }
}
