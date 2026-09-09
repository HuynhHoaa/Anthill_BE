namespace EduQuiz.Application.DTOs.ExamAttempts;

/// <summary>
/// Đồng bộ đồng hồ với giờ server — <c>GET /api/exam-attempts/{attemptId}/time</c>.
/// </summary>
/// <remarks>
/// Endpoint bổ sung so với frontend, và cố ý <b>nhẹ</b>: không nạp đề, không đọc câu trả lời. Màn
/// hình làm bài gọi nó vài phút một lần chỉ để chỉnh lại con số đếm ngược, nên nó không được kéo
/// theo cả bản đề mỗi lần.
/// </remarks>
public sealed record AttemptTimeDto
{
    public required DateTime ServerNow { get; init; }

    public required DateTime ExpiresAt { get; init; }

    /// <summary>Bằng 0 khi đã hết giờ — không bao giờ âm.</summary>
    public required int RemainingSeconds { get; init; }

    public required string Status { get; init; }
}
