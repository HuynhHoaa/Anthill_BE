using Anthill.Domain.Common;

namespace Anthill.Domain.Entities;

/// <summary>
/// Lựa chọn của học sinh cho một câu trong đề đã sinh. Duy nhất theo cặp
/// (AttemptId, ExamPaperQuestionId) vì lưu nháp mang ngữ nghĩa MERGE; câu nhiều đáp án đúng giữ cả tập
/// phương án đã chọn trong một dòng.
/// </summary>
public class StudentAnswer : BaseEntity
{
    public int AttemptId { get; set; }

    public int ExamPaperQuestionId { get; set; }

    /// <summary>
    /// Id các <see cref="ExamPaperAnswer"/> (bản snapshot, không phải <see cref="AnswerOption"/> gốc) mà học
    /// sinh đã chọn. Rỗng = bỏ trống câu này. Câu một đáp án đúng có tối đa một phần tử.
    /// </summary>
    /// <remarks>
    /// Là cột mảng chứ không phải một dòng cho mỗi lựa chọn: lưu nháp ghi đè <b>cả tập</b> của một câu trong
    /// một lần, nên một dòng mỗi câu giữ được ràng buộc duy nhất theo (lượt, câu) mà không phải xoá rồi thêm
    /// lại từng dòng. Không có khoá ngoại tới từng phần tử; tính hợp lệ do tầng service kiểm lúc lưu, và bản
    /// đề không bao giờ bị xoá riêng phần đáp án (xoá bản đề kéo theo cả các dòng này).
    /// </remarks>
    public int[] SelectedAnswerIds { get; set; } = [];

    public ExamAttempt Attempt { get; set; } = null!;

    public ExamPaperQuestion ExamPaperQuestion { get; set; } = null!;
}
