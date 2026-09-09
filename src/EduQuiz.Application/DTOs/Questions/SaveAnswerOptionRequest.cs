namespace EduQuiz.Application.DTOs.Questions;

/// <summary>
/// Một đáp án trong body tạo/sửa câu hỏi.
/// </summary>
/// <remarks>
/// <para>
/// Cố ý <b>không</b> có <c>Id</c>. Sửa câu hỏi là <b>thay cả mảng đáp án</b> chứ không sửa tại chỗ
/// (§4.5), nên id cũ không có ý nghĩa gì ở đường vào — server xoá hết đáp án cũ rồi cấp id mới.
/// Nhận id rồi bỏ qua sẽ khiến người tích hợp tưởng mình đang sửa từng đáp án.
/// </para>
/// <para>
/// Cũng không có <c>Ordinal</c>: thứ tự chính là thứ tự phần tử trong mảng. Nhận thêm một trường
/// thứ tự nghĩa là phải xử lý cả trường hợp nó mâu thuẫn với thứ tự mảng.
/// </para>
/// </remarks>
public sealed record SaveAnswerOptionRequest
{
    /// <summary>Có thể để trống <b>nếu</b> đáp án có ảnh — đáp án chỉ gồm một hình là hợp lệ.</summary>
    public string? Content { get; init; }

    /// <summary>
    /// URL trả về từ <c>POST /api/uploads/images</c>. <b>Không nhận data URI</b> (§1) — frontend
    /// hiện nhét thẳng chuỗi <c>data:image/png;base64,...</c> vào đây và phải đổi.
    /// </summary>
    public string? ImageUrl { get; init; }

    public bool IsCorrect { get; init; }
}
