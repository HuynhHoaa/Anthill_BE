namespace Anthill.Application.DTOs.Questions;

/// <summary>
/// Một đáp án dưới góc nhìn <b>người soạn đề</b>. Khớp interface <c>AnswerOption</c> của frontend.
/// </summary>
/// <remarks>
/// <para>
/// <b>Có</b> trường <see cref="IsCorrect"/>, và ở đây điều đó là đúng: màn hình soạn câu hỏi phải
/// hiện dấu tích vào đáp án đúng thì giáo viên mới sửa được. Người gọi mọi endpoint dùng DTO này
/// đều là giáo viên hoặc quản trị viên, không phải học sinh.
/// </para>
/// <para>
/// <b>Đừng dùng lại DTO này cho màn hình làm bài.</b> §3 quy tắc ① đòi lúc học sinh làm bài phải là
/// một kiểu <i>không khai báo</i> trường <c>isCorrect</c> — gán <c>false</c> không đủ, vì học sinh
/// mở DevTools là thấy toàn bộ response. Bước 6 sẽ có <c>AttemptAnswerOptionDto</c> riêng cho việc
/// đó, và <c>ResultAnswerOptionDto</c> cho lúc xem lại kết quả.
/// </para>
/// </remarks>
public sealed record AnswerOptionDto
{
    public required int Id { get; init; }

    public required string Content { get; init; }

    /// <summary>URL ảnh minh hoạ. Bỏ hẳn khỏi JSON khi không có.</summary>
    public string? ImageUrl { get; init; }

    public required bool IsCorrect { get; init; }
}
