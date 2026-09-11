namespace Anthill.Domain.Common;

/// <summary>
/// Gốc chung cho mọi entity: khoá chính int tự tăng và hai mốc thời gian UTC.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    /// <summary>Luôn do server đặt bằng <c>DateTime.UtcNow</c>, không nhận từ client.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>NULL khi bản ghi chưa từng được sửa.</summary>
    public DateTime? UpdatedAt { get; set; }
}
