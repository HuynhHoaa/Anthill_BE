using Anthill.Domain.Common;
using Anthill.Domain.Enums;

namespace Anthill.Domain.Entities;

/// <summary>
/// Thông báo luôn thuộc về <b>một người cụ thể</b>, không phát theo vai trò —
/// nhờ đó truy vấn và kiểm tra quyền sở hữu chỉ là một điều kiện trên khoá ngoại.
/// </summary>
public class Notification : BaseEntity
{
    public int RecipientUserId { get; set; }

    public NotificationType Type { get; set; }

    public required string Title { get; set; }

    public required string Message { get; set; }

    /// <summary>Đường dẫn tương đối trong SPA để bấm vào là nhảy tới đúng màn hình.</summary>
    public string? Link { get; set; }

    /// <summary>Cặp TargetType/TargetId cho phép điều hướng lại khi đường dẫn SPA đổi.</summary>
    public string? TargetType { get; set; }

    public int? TargetId { get; set; }

    public bool IsRead { get; set; }

    public User Recipient { get; set; } = null!;
}
