namespace Anthill.Application.DTOs.ExamAttempts;

/// <summary>Một lựa chọn của học sinh: câu nào, ô nào.</summary>
/// <remarks>
/// Client chỉ được gửi đúng hai con số này. Mọi thứ suy ra từ chúng — đúng hay sai, được bao nhiêu
/// điểm — đều do server tính từ bản chụp đề (§3 quy tắc ②).
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
}
