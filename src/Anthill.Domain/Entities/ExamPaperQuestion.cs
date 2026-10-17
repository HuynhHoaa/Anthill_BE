using Anthill.Domain.Common;

namespace Anthill.Domain.Entities;

/// <summary>
/// Một câu trong đề đã sinh. Nội dung được <b>chụp lại</b> chứ không đọc qua
/// <see cref="Question"/>: giáo viên sửa câu hỏi về sau không được làm đổi bài đã chấm.
/// </summary>
public class ExamPaperQuestion : BaseEntity
{
    public int ExamPaperId { get; set; }

    /// <summary>Chỉ để truy vết nguồn và thống kê. Hiển thị và chấm điểm đều dùng snapshot.</summary>
    public int QuestionId { get; set; }

    /// <summary>Thứ tự đã chốt lúc sinh đề (Fisher-Yates có seed). Không xáo lại khi trả response.</summary>
    public int Ordinal { get; set; }

    public required string ContentSnapshot { get; set; }

    public string? ImageUrlSnapshot { get; set; }

    /// <summary>Điểm của câu tại thời điểm sinh đề.</summary>
    public decimal Point { get; set; }

    /// <summary>Câu cho chọn nhiều đáp án, chụp lúc sinh đề cùng với nội dung và đáp án.</summary>
    public bool AllowMultiple { get; set; }

    public ExamPaper ExamPaper { get; set; } = null!;

    public Question Question { get; set; } = null!;

    public ICollection<ExamPaperAnswer> Answers { get; set; } = new List<ExamPaperAnswer>();

    public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
}
