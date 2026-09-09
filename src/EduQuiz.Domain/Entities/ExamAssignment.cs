using EduQuiz.Domain.Common;

namespace EduQuiz.Domain.Entities;

/// <summary>
/// Việc giao một đề cho một học sinh. Duy nhất theo cặp (ExamId, StudentId) để
/// thao tác giao bài là idempotent.
/// </summary>
public class ExamAssignment : BaseEntity
{
    public int ExamId { get; set; }

    public int StudentId { get; set; }

    /// <summary>Mốc nghiệp vụ hiển thị cho giáo viên; <c>CreatedAt</c> ở lớp cơ sở là cột audit.</summary>
    public DateTime AssignedAt { get; set; }

    public Exam Exam { get; set; } = null!;

    public User Student { get; set; } = null!;

    /// <summary>Tối đa một lượt làm bài — v1 không cho làm lại.</summary>
    public ExamAttempt? Attempt { get; set; }

    /// <summary>
    /// Đề riêng của học sinh này, chỉ tồn tại khi <c>Exam.RandomizePerStudent = true</c>.
    /// Ngược lại dùng đề chung của kỳ thi (ExamPaper có AssignmentId = NULL).
    /// </summary>
    public ExamPaper? Paper { get; set; }
}
