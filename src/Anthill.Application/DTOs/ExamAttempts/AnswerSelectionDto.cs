namespace Anthill.Application.DTOs.ExamAttempts;

/// <summary>Một lựa chọn của học sinh: câu nào, những ô nào.</summary>
/// <remarks>
/// Client chỉ được gửi mã câu và mã các ô đã chọn. Mọi thứ suy ra từ chúng — đúng hay sai, được bao
/// nhiêu điểm — đều do server tính từ bản chụp đề (§3 quy tắc ②).
/// </remarks>
public sealed record AnswerSelectionDto
{
    /// <summary>Id của câu <b>trong bản đề này</b> (<c>ExamPaperQuestion</c>), không phải id câu hỏi gốc.</summary>
    public required int QuestionId { get; init; }

    /// <summary>
    /// Id phương án đã chọn, hoặc NULL khi học sinh bỏ chọn.
    /// </summary>
    /// <remarks>
    /// Cho phép NULL vì bỏ chọn là một thao tác thật trên màn hình làm bài. Không có nó thì câu đã
    /// lỡ tích không bao giờ trở về trạng thái trắng được nữa.
    /// </remarks>
    public int? AnswerId { get; init; }

    /// <summary>
    /// Tập id phương án đã chọn. Dùng cho câu nhiều đáp án đúng; câu một đáp án đúng nhận tối đa một phần tử.
    /// Mảng rỗng là bỏ chọn hết.
    /// </summary>
    /// <remarks>
    /// Chỉ gửi <see cref="AnswerId"/> <b>hoặc</b> <see cref="AnswerIds"/>, không gửi cả hai: hai trường nói hai
    /// điều khác nhau thì không có câu trả lời đúng nào cho "em chọn ô nào", và im lặng ưu tiên một bên là
    /// chấm một đáp án em không chọn.
    /// </remarks>
    public IReadOnlyList<int>? AnswerIds { get; init; }
}
