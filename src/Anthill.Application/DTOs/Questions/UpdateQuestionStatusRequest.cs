namespace Anthill.Application.DTOs.Questions;

/// <summary>
/// Thân request của <c>PATCH /api/questions/{id}/status</c> — bật hoặc ẩn nhanh một câu hỏi.
/// </summary>
/// <remarks>
/// Tách khỏi <see cref="SaveQuestionRequest"/> vì đây là một nút gạt trên bảng danh sách. Gộp vào
/// <c>PUT</c> thì mỗi lần ẩn một câu, frontend phải gửi kèm cả nội dung lẫn toàn bộ mảng đáp án —
/// và vì <c>PUT</c> thay cả mảng đáp án, một cú gạt nhầm sẽ cấp lại id cho toàn bộ đáp án.
/// </remarks>
public sealed record UpdateQuestionStatusRequest
{
    /// <summary><c>"active"</c> hoặc <c>"hidden"</c>.</summary>
    public required string Status { get; init; }
}
