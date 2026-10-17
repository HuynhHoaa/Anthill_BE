using Anthill.Domain.Common;

namespace Anthill.Domain.Entities;

/// <summary>
/// Một đề nhỏ của một lớp trên một đề được chia thành nhiều đề: danh sách mã câu hỏi đã chia sẵn.
/// </summary>
/// <remarks>
/// <para>
/// Mỗi lớp có bộ đề nhỏ riêng (chia lại, xáo riêng) nên lớp này khó chép đề lớp kia. Bộ đề nhỏ nằm ở bảng riêng chứ
/// không suy lại từ bản đề của từng em vì: lớp có 2 em mà chia 4 đề thì 2 đề chưa em nào cầm, và em vào lớp sau cần nhận
/// đúng đề nhỏ đã chia chứ không phải một cách chia mới.
/// </para>
/// <para>
/// Bản đề của từng em (<see cref="ExamPaper"/>) vẫn là bản chụp riêng (xáo thứ tự câu, thứ tự đáp án theo từng em) chép từ
/// danh sách này; bảng này chỉ nhớ <i>câu nào thuộc đề nhỏ nào</i>.
/// </para>
/// </remarks>
public class ExamPaperVariant : BaseEntity
{
    public int ExamId { get; set; }

    /// <summary>Lớp mà bộ đề nhỏ này chia cho. NULL chỉ với dữ liệu giao lẻ cũ không gắn lớp.</summary>
    public int? GroupId { get; set; }

    /// <summary>Số thứ tự đề từ 0 (0 = đề A).</summary>
    public int VariantIndex { get; set; }

    /// <summary>Mã các câu hỏi của đề nhỏ (không theo thứ tự hiển thị — thứ tự do bản đề của từng em chốt).</summary>
    public List<int> QuestionIds { get; set; } = [];

    public Exam Exam { get; set; } = null!;

    public StudentGroup? Group { get; set; }
}
