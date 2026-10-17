namespace Anthill.Application.DTOs.ExamAttempts;

/// <summary>Một câu trong đề, như học sinh nhìn thấy lúc làm bài.</summary>
/// <remarks>
/// Nội dung lấy từ bản chụp trong <c>ExamPaperQuestion</c> chứ không đọc qua ngân hàng câu hỏi:
/// giáo viên sửa hoặc xoá câu hỏi giữa lúc cả lớp đang thi cũng không làm đổi đề trên màn hình các em.
/// </remarks>
public sealed record AttemptQuestionDto
{
    /// <summary>
    /// Id của <c>ExamPaperQuestion</c>, tức <b>câu trong bản đề này</b> chứ không phải câu trong
    /// ngân hàng.
    /// </summary>
    /// <remarks>
    /// Đây là giá trị client gửi lại ở trường <c>questionId</c> khi lưu nháp và khi nộp. Dùng id
    /// của ngân hàng thay cho id này sẽ hỏng ngay khi bật <c>randomizePerStudent</c>: hai em có thể
    /// cùng nhận một câu hỏi gốc nhưng thứ tự phương án khác nhau, nên cặp (câu, đáp án) chỉ có
    /// nghĩa trong phạm vi một bản đề.
    /// </remarks>
    public required int Id { get; init; }

    /// <summary>Thứ tự đã chốt lúc sinh đề. Không xáo lại ở tầng này (§5).</summary>
    public required int Ordinal { get; init; }

    public required string Content { get; init; }

    public string? ImageUrl { get; init; }

    /// <summary>Điểm của câu, chốt lúc sinh đề — hiện lên để học sinh biết câu nào nặng hơn.</summary>
    public required decimal Point { get; init; }

    /// <summary>
    /// Câu cho chọn nhiều đáp án: màn hình làm bài vẽ ô tích thay cho ô chọn một, và học sinh phải chọn đúng
    /// và đủ mới được điểm. Chỉ nói "được chọn nhiều ô" chứ không nói có bao nhiêu ô đúng.
    /// </summary>
    public required bool AllowMultiple { get; init; }

    public required IReadOnlyList<AttemptAnswerOptionDto> Answers { get; init; }
}
