using EduQuiz.Domain.Common;

namespace EduQuiz.Domain.Entities;

public class StudentGroup : BaseEntity
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Giáo viên sở hữu nhóm. Quyết định ai được giao bài cho nhóm này.</summary>
    public int OwnerTeacherId { get; set; }

    public User OwnerTeacher { get; set; } = null!;

    /// <summary>Sĩ số luôn đếm qua đây, không lưu cứng thành cột.</summary>
    public ICollection<User> Students { get; set; } = new List<User>();
}
