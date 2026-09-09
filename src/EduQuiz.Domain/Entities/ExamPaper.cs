using EduQuiz.Domain.Common;

namespace EduQuiz.Domain.Entities;

/// <summary>
/// Bản chụp một đề đã sinh: thứ tự câu, thứ tự đáp án và nội dung tại thời điểm sinh.
/// Sinh một lần rồi bất biến — học sinh F5 hay đổi thiết bị vẫn thấy đúng bộ câu cũ.
/// </summary>
public class ExamPaper : BaseEntity
{
    public int ExamId { get; set; }

    /// <summary>NULL = đề chung cho cả lớp (<c>Exam.RandomizePerStudent = false</c>).</summary>
    public int? AssignmentId { get; set; }

    public Exam Exam { get; set; } = null!;

    public ExamAssignment? Assignment { get; set; }

    public ICollection<ExamPaperQuestion> Questions { get; set; } = new List<ExamPaperQuestion>();
}
