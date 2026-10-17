using Anthill.Domain.Common;

namespace Anthill.Domain.Entities;

/// <summary>
/// Bản chụp một đề đã sinh: thứ tự câu, thứ tự đáp án và nội dung tại thời điểm sinh.
/// Sinh một lần rồi bất biến — học sinh F5 hay đổi thiết bị vẫn thấy đúng bộ câu cũ.
/// </summary>
public class ExamPaper : BaseEntity
{
    public int ExamId { get; set; }

    /// <summary>NULL = đề chung cho cả lớp (<c>Exam.RandomizePerStudent = false</c>).</summary>
    public int? AssignmentId { get; set; }

    /// <summary>
    /// Đề nhỏ (số thứ tự từ 0: 0 = đề A, 1 = đề B…) mà bản đề này chép từ đó, khi <c>Exam.PaperCount &gt; 1</c>.
    /// NULL với đề không chia. Chỉ giáo viên thấy mã đề; học sinh không cần biết mình cầm đề nào.
    /// </summary>
    public int? VariantIndex { get; set; }

    public Exam Exam { get; set; } = null!;

    public ExamAssignment? Assignment { get; set; }

    public ICollection<ExamPaperQuestion> Questions { get; set; } = new List<ExamPaperQuestion>();
}
