using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>
/// Một lượt làm bài đã khép lại <b>vì hết giờ</b> hay không — chỗ quyết định giữa 410 và 409.
/// </summary>
/// <remarks>
/// <para>
/// Một lượt quá hạn có hai đường để được chốt sổ: request đầu tiên chạm vào nó sau giờ hết, hoặc
/// <c>AutoSubmitExpiredAttemptsService</c> quét trúng trước. Trước đây câu trả lời phụ thuộc vào
/// đường nào thắng: request tự chốt thì trả 410 "hết giờ", còn tiến trình nền chốt trước vài mili
/// giây thì cùng lời gọi đó nhận 409 "lượt đã kết thúc". Cùng một sự thật mà hai mã khác nhau tuỳ
/// vào một cuộc đua client không nhìn thấy — client buộc phải coi cả hai là "hết giờ", và 409 mất
/// luôn ý nghĩa riêng của nó.
/// </para>
/// <para>
/// Phân biệt bằng cờ <c>AutoSubmitted</c> chứ không bằng <c>SubmittedAt</c> so với <c>ExpiresAt</c>:
/// <c>GradeAsync</c> bật cờ đó cho mọi lần chấm sau giờ hết — tiến trình nền, request tự chốt, và
/// cả học sinh bấm Nộp khi chuông đã reo. Cả ba đều là "thời gian đã hết", nên đều ra 410. Chỉ bài
/// em tự nộp trước giờ mới giữ 409: với bài đó, câu đúng là "em đã nộp rồi" chứ không phải
/// "hết giờ", dù đồng hồ lúc này có qua <c>ExpiresAt</c> hay chưa.
/// </para>
/// <para>
/// Hàm thuần để kiểm được cả ma trận trạng thái bằng unit test mà không phải dựng lại cuộc đua.
/// </para>
/// </remarks>
public static class AttemptClosure
{
    /// <param name="status">Trạng thái lượt, đọc dưới khoá ghi.</param>
    /// <param name="autoSubmitted">Cờ <c>ExamAttempt.AutoSubmitted</c>.</param>
    /// <param name="expiresAtUtc">Hạn giờ đã chốt lúc bắt đầu.</param>
    /// <param name="nowUtc">Giờ server.</param>
    /// <remarks>
    /// Bài tự nộp vẫn phải thật sự quá <c>ExpiresAt</c> mới tính là hết giờ. Theo cách ghi hiện tại
    /// thì cờ đó không bao giờ bật trước hạn, nhưng đồng hồ của hai instance API có thể lệch nhau
    /// vài mili giây; khi đó "hết giờ" mà đồng hồ của chính request này chưa thấy thì báo 409 an
    /// toàn hơn một lời khẳng định sai.
    /// </remarks>
    public static bool ClosedByDeadline(
        AssignmentStatus status,
        bool autoSubmitted,
        DateTime expiresAtUtc,
        DateTime nowUtc) => status switch
    {
        // Còn mở mà đã qua hạn: người gọi chốt sổ trước rồi mới báo hết giờ.
        AssignmentStatus.InProgress => AttemptDeadline.IsExpired(expiresAtUtc, nowUtc),

        // Đánh dấu hết hạn mà không có điểm: đóng vì thời gian, không phải vì em nộp.
        AssignmentStatus.Expired => true,

        AssignmentStatus.Submitted or AssignmentStatus.Graded =>
            autoSubmitted && AttemptDeadline.IsExpired(expiresAtUtc, nowUtc),

        _ => false
    };
}
