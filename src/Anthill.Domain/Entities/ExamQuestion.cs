using Anthill.Domain.Common;

namespace Anthill.Domain.Entities;

/// <summary>
/// Một câu hỏi nằm trong danh sách câu của đề, kèm điểm của riêng câu đó.
/// </summary>
/// <remarks>
/// <para>
/// Chỉ có ở đề soạn theo danh sách (<see cref="Exam.UsesQuestionList"/>). Đây là danh sách giáo viên đã
/// duyệt: học sinh nhận đúng những câu này. Khác với <see cref="ExamPaperQuestion"/>: dòng ở đây là một
/// <b>con trỏ</b> tới câu hỏi trong ngân hàng, còn bản đề mới là bản chụp nội dung lúc giao bài.
/// </para>
/// <para>
/// Điểm nằm ở đây chứ không lấy từ <c>Question.DefaultPoint</c>: cùng một câu có thể đáng 1 điểm ở bài 15
/// phút và 5 điểm ở bài một tiết.
/// </para>
/// </remarks>
public class ExamQuestion : BaseEntity
{
    public int ExamId { get; set; }

    public int QuestionId { get; set; }

    /// <summary>Thứ tự giáo viên xếp trong đề, bắt đầu từ 0. Bản đề chỉ giữ thứ tự này khi tắt trộn câu hỏi.</summary>
    public int Ordinal { get; set; }

    /// <summary>Điểm của câu này trong đề. Tổng các câu là tổng điểm của đề.</summary>
    public decimal Point { get; set; }

    public Exam Exam { get; set; } = null!;

    public Question Question { get; set; } = null!;
}
