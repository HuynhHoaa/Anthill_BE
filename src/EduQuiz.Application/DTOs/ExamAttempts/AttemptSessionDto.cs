namespace EduQuiz.Application.DTOs.ExamAttempts;

/// <summary>
/// Trọn một phiên làm bài: đề, đồng hồ và những gì học sinh đã chọn tới lúc này.
/// </summary>
/// <remarks>
/// Cùng một hình dạng được trả về ở cả <c>POST /api/exam-attempts</c> lẫn
/// <c>GET /api/exam-attempts/{attemptId}</c>. Đó là điều kiện để màn hình làm bài dựng lại chính
/// nó sau F5 hay sau khi đổi thiết bị mà không cần biết mình vừa bắt đầu hay đang làm tiếp.
/// </remarks>
public sealed record AttemptSessionDto
{
    public required int AttemptId { get; init; }

    public required int AssignmentId { get; init; }

    public required int ExamId { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required int DurationMinutes { get; init; }

    public required DateTime StartedAt { get; init; }

    /// <summary>Hạn giờ chốt trong database. Client vẽ đồng hồ theo giá trị này, không theo giờ máy mình.</summary>
    public required DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Giờ server tại thời điểm trả response.
    /// </summary>
    /// <remarks>
    /// Có cặp <c>serverNow</c> + <c>expiresAt</c> thì client tự tính được độ lệch đồng hồ của máy
    /// mình mà không cần tin vào nó (§3 quy tắc ③).
    /// </remarks>
    public required DateTime ServerNow { get; init; }

    public required int RemainingSeconds { get; init; }

    /// <summary>Luôn là <c>"inprogress"</c> — phiên đã nộp không đi qua endpoint này nữa.</summary>
    public required string Status { get; init; }

    public required int TotalQuestions { get; init; }

    /// <summary>Tổng điểm của bản đề, để màn hình hiện "x/10" ngay từ lúc bắt đầu.</summary>
    public required decimal MaxScore { get; init; }

    public required IReadOnlyList<AttemptQuestionDto> Questions { get; init; }

    /// <summary>
    /// Những lựa chọn đã lưu: khoá là <c>questionId</c>, giá trị là <c>answerId</c>.
    /// </summary>
    /// <remarks>
    /// Câu chưa chọn thì <b>vắng mặt</b> khỏi từ điển chứ không mang giá trị null — trạng thái
    /// "chưa trả lời" và "đã xoá lựa chọn" là một, và một khoá có giá trị null chỉ khiến client phải
    /// viết thêm nhánh kiểm tra cho đúng cái tình huống đó.
    /// </remarks>
    public required IReadOnlyDictionary<int, int> SavedAnswers { get; init; }
}
