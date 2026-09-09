using EduQuiz.Domain.Common;

namespace EduQuiz.Domain.Entities;

/// <summary>
/// Lựa chọn của học sinh cho một câu trong đề đã sinh. Duy nhất theo cặp
/// (AttemptId, ExamPaperQuestionId) vì lưu nháp mang ngữ nghĩa MERGE.
/// </summary>
public class StudentAnswer : BaseEntity
{
    public int AttemptId { get; set; }

    public int ExamPaperQuestionId { get; set; }

    /// <summary>
    /// Trỏ tới <see cref="ExamPaperAnswer"/> (bản snapshot), không phải
    /// <see cref="AnswerOption"/> gốc. NULL = học sinh bỏ trống câu này.
    /// </summary>
    public int? SelectedAnswerId { get; set; }

    public ExamAttempt Attempt { get; set; } = null!;

    public ExamPaperQuestion ExamPaperQuestion { get; set; } = null!;

    public ExamPaperAnswer? SelectedAnswer { get; set; }
}
