using EduQuiz.Domain.Common;

namespace EduQuiz.Domain.Entities;

/// <summary>
/// Một phương án trong đề đã sinh. Id của bản ghi này chính là <c>answerId</c> mà
/// học sinh gửi lên khi chọn đáp án.
/// </summary>
public class ExamPaperAnswer : BaseEntity
{
    public int ExamPaperQuestionId { get; set; }

    public required string ContentSnapshot { get; set; }

    public string? ImageUrlSnapshot { get; set; }

    /// <summary>
    /// Nguồn sự thật duy nhất khi chấm — không bao giờ tra ngược
    /// <see cref="AnswerOption"/> gốc, vì nó có thể đã bị sửa sau khi học sinh nộp bài.
    /// </summary>
    public bool IsCorrect { get; set; }

    public int Ordinal { get; set; }

    public ExamPaperQuestion ExamPaperQuestion { get; set; } = null!;
}
