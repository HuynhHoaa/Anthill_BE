namespace EduQuiz.Application.DTOs.ExamAttempts;

/// <summary>
/// Một phương án dưới góc nhìn <b>học sinh đang làm bài</b>.
/// </summary>
/// <remarks>
/// <para>
/// Kiểu này cố ý <b>không khai báo</b> trường <c>isCorrect</c> — đó là toàn bộ lý do nó tồn tại
/// (§3 quy tắc ①). Frontend hiện gán <c>isCorrect = false</c> trước khi trả câu hỏi xuống, nhưng
/// gán false không đủ: học sinh mở DevTools là thấy nguyên văn response, và một trường luôn bằng
/// false ở mọi phương án trừ một chỗ nào đó thì tự nó là câu trả lời. Chỉ khi trường đó
/// <i>không có mặt</i> trong JSON thì mới không có gì để đọc.
/// </para>
/// <para>
/// Đừng dùng lại <c>AnswerOptionDto</c> của ngân hàng câu hỏi cho màn hình làm bài, và cũng đừng
/// thêm <c>isCorrect</c> vào đây "cho tiện lúc xem lại kết quả" — việc xem lại có DTO riêng ở §4.8,
/// nơi quyền xem được kiểm trước khi trả về.
/// </para>
/// </remarks>
public sealed record AttemptAnswerOptionDto
{
    /// <summary>
    /// Id của <c>ExamPaperAnswer</c> (bản chụp), không phải <c>AnswerOption</c> gốc. Đây chính là
    /// giá trị học sinh gửi lại ở <c>answerId</c> khi chọn đáp án.
    /// </summary>
    public required int Id { get; init; }

    public required string Content { get; init; }

    /// <summary>URL ảnh minh hoạ. Bỏ hẳn khỏi JSON khi không có.</summary>
    public string? ImageUrl { get; init; }
}
