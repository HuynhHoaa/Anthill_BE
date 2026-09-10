using EduQuiz.Application.DTOs.Notifications;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Mappings;

/// <summary>Chuyển <see cref="Notification"/> sang DTO.</summary>
public static class NotificationMappingExtensions
{
    public static NotificationDto ToDto(this Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return new NotificationDto
        {
            Id = notification.Id,
            Type = notification.Type.ToNotificationTypeString(),
            Title = notification.Title,
            Message = notification.Message,
            Link = notification.Link,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt.ToUtc()
        };
    }

    /// <summary>Tên sự kiện viết kiểu camelCase, khớp với tên sự kiện SignalR ở §7.</summary>
    /// <remarks>
    /// Cùng một chuỗi dùng cho cả bản ghi trong database lẫn tên sự kiện realtime, để giao diện chỉ
    /// phải học một bộ tên. Thông báo đến qua hai đường — nạp lại trang và đẩy realtime — mà hai
    /// đường đó gọi cùng một thứ bằng hai cái tên thì màn hình phải có hai nhánh xử lý.
    /// </remarks>
    public static string ToNotificationTypeString(this NotificationType type) => type switch
    {
        NotificationType.ExamAssigned => "examAssigned",
        NotificationType.ExamSubmitted => "examSubmitted",
        NotificationType.ExamUpdated => "examUpdated",
        NotificationType.DeadlineSoon => "deadlineSoon",
        NotificationType.AutoSubmitted => "autoSubmitted",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
}
