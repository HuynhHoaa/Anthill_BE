using EduQuiz.Domain.Common;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Domain.Entities;

/// <summary>
/// Câu hỏi trắc nghiệm một đáp án đúng. Hai cột phân định tách bạch nhau:
/// <see cref="SubjectId"/> quyết định <i>ai được thấy</i>, <see cref="CreatedByUserId"/>
/// quyết định <i>ai được sửa</i>.
/// </summary>
public class Question : BaseEntity
{
    public required string Content { get; set; }

    public string? ImageUrl { get; set; }

    public required string Topic { get; set; }

    public QuestionDifficulty Difficulty { get; set; }

    public decimal DefaultPoint { get; set; }

    public QuestionStatus Status { get; set; } = QuestionStatus.Active;

    /// <summary>PHẠM VI: ngân hàng câu hỏi mà câu này thuộc về; lọc và chặn giáo viên môn khác.</summary>
    public int SubjectId { get; set; }

    /// <summary>QUY TRÁCH: hiển thị "soạn bởi", giới hạn quyền ghi, phục vụ audit.</summary>
    public int CreatedByUserId { get; set; }

    public QuestionVisibility Visibility { get; set; } = QuestionVisibility.Shared;

    public Subject Subject { get; set; } = null!;

    public User CreatedBy { get; set; } = null!;

    public ICollection<AnswerOption> Answers { get; set; } = new List<AnswerOption>();

    /// <summary>
    /// Các lần câu hỏi này được đưa vào một đề đã sinh. Tồn tại để chặn xoá câu hỏi
    /// đang nằm trong đề hoặc bài làm — không dùng để chấm điểm.
    /// </summary>
    public ICollection<ExamPaperQuestion> PaperQuestions { get; set; } = new List<ExamPaperQuestion>();
}
