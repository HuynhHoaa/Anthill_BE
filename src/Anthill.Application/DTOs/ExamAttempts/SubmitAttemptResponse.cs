namespace Anthill.Application.DTOs.ExamAttempts;

/// <summary>Kết quả trả về ngay sau khi nộp bài.</summary>
/// <remarks>
/// <para>
/// Đủ cho màn hình "Đã nộp bài" mà <b>không</b> kèm chi tiết từng câu: xem lại từng câu đi qua
/// <c>GET /api/results/{attemptId}</c> ở §4.8, nơi quyền xem đáp án được kiểm riêng (§3 quy tắc ⑥).
/// </para>
/// <para>
/// Nộp lại cùng một <c>attemptId</c> trả về đúng bản này với đúng những con số cũ — nộp bài là
/// idempotent, vì retry sau timeout mạng vẫn tới server hai lần dù frontend có cờ chống bấm đúp (§5).
/// </para>
/// </remarks>
public sealed record SubmitAttemptResponse
{
    public required int AttemptId { get; init; }

    public required int ExamId { get; init; }

    /// <summary>Luôn là <c>"graded"</c>: trắc nghiệm một đáp án đúng thì chấm xong ngay trong lời gọi này.</summary>
    public required string Status { get; init; }

    public required DateTime SubmittedAt { get; init; }

    /// <summary>
    /// Bài do hệ thống nộp thay vì học sinh bấm nộp.
    /// </summary>
    /// <remarks>
    /// Bật trong hai tình huống: background job quét thấy hết giờ, và học sinh bấm Nộp sau khi
    /// chuông đã reo — ở tình huống thứ hai server chấm phần đã lưu và bỏ qua phần gửi kèm muộn,
    /// nên nói rằng em tự nộp là không đúng sự thật.
    /// </remarks>
    public required bool AutoSubmitted { get; init; }

    public required int TotalQuestions { get; init; }

    public required int AnsweredCount { get; init; }

    /// <summary>Tổng điểm của đề. Luôn có mặt — biết thang điểm không làm lộ bài làm của ai.</summary>
    public required decimal MaxScore { get; init; }

    /// <summary>Giáo viên có cho xem kết quả ngay không. Quyết định ba trường bên dưới có mặt hay không.</summary>
    public required bool ShowResultAfterSubmit { get; init; }

    /// <summary>Vắng mặt khỏi JSON khi giáo viên tắt <c>showResultAfterSubmit</c> (§3 quy tắc ⑥).</summary>
    public decimal? Score { get; init; }

    /// <summary>Cùng điều kiện với <see cref="Score"/>.</summary>
    public int? CorrectCount { get; init; }
}
