using Anthill.Domain.Common;
using Anthill.Domain.Enums;

namespace Anthill.Domain.Entities;

/// <summary>
/// Câu hỏi trắc nghiệm một đáp án đúng. Hai cột phân định tách bạch nhau:
/// <see cref="SubjectId"/> quyết định <i>ai được thấy</i>, <see cref="CreatedByUserId"/>
/// quyết định <i>ai được sửa</i>.
/// </summary>
public class Question : BaseEntity
{
    /// <summary>
    /// Tên ngắn để nhận ra câu hỏi trong ngân hàng và trong đề. Tách khỏi <see cref="Content"/> vì nội dung
    /// có thể là cả một đoạn dữ kiện, còn tên chỉ cần vài chữ. Giáo viên không đặt thì để rỗng — nơi hiển thị
    /// lúc đó rơi về phần đầu của nội dung, nên không cần giá trị sinh sẵn nằm trong database.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    public required string Content { get; set; }

    public string? ImageUrl { get; set; }

    public required string Topic { get; set; }

    public QuestionDifficulty Difficulty { get; set; }

    /// <summary>
    /// Câu cho chọn nhiều đáp án: có từ một đáp án đúng trở lên, học sinh phải chọn <b>đúng và đủ</b> mới
    /// được điểm. Mặc định là câu một đáp án đúng.
    /// </summary>
    /// <remarks>
    /// Là lựa chọn rõ ràng của giáo viên chứ không suy ra từ số đáp án đúng: câu "chọn tất cả đáp án đúng"
    /// vẫn có thể chỉ có một đáp án đúng, và học sinh lúc làm bài cần biết mình được chọn nhiều ô.
    /// </remarks>
    public bool AllowMultiple { get; set; }

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

    /// <summary>Các đề soạn theo danh sách đang chứa câu hỏi này. Cũng chặn xoá câu hỏi.</summary>
    public ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
}
