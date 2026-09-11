using Anthill.Domain.Common;

namespace Anthill.Domain.Entities;

/// <summary>
/// Môn học — đồng thời là ranh giới của một ngân hàng câu hỏi dùng chung theo tổ bộ môn.
/// </summary>
public class Subject : BaseEntity
{
    public required string Name { get; set; }

    public required string Code { get; set; }

    /// <summary>Giáo viên thuộc môn này.</summary>
    public ICollection<User> Teachers { get; set; } = new List<User>();

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
