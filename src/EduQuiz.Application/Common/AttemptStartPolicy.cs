using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Common;

/// <summary>Việc phải làm khi học sinh gọi <c>POST /api/exam-attempts</c>.</summary>
public enum AttemptStartDecision
{
    /// <summary>Chưa có lượt nào và đề đang mở — sinh đề rồi bấm giờ.</summary>
    CreateNew,

    /// <summary>Đã có lượt còn hạn — trả lại đúng phiên đó, không đụng vào đồng hồ.</summary>
    Resume,

    /// <summary>Còn dở nhưng đã quá <c>ExpiresAt</c> — chấm nốt phần đã lưu rồi báo hết giờ.</summary>
    Finalise,

    /// <summary>Đã nộp rồi. V1 không cho làm lại.</summary>
    AlreadyFinished,

    /// <summary>Đề còn là nháp hoặc chưa tới giờ mở.</summary>
    NotOpenYet,

    /// <summary>Đề đã đóng mà em chưa từng bắt đầu.</summary>
    Closed
}

/// <summary>
/// Một chỗ duy nhất trả lời "học sinh này có được vào làm bài không, và vào thì thấy gì".
/// </summary>
/// <remarks>
/// <para>
/// Tách khỏi service vì đây là phần khiến <c>POST /api/exam-attempts</c> <b>idempotent</b> (§4.7):
/// gọi lại khi đang làm dở phải trả về chính lượt cũ, không tạo lượt thứ hai và không cấp thêm giờ.
/// Học sinh F5, đổi thiết bị, hoặc mạng chập chờn khiến trình duyệt gửi lại request — cả ba đều
/// dẫn tới đây, và cả ba đều phải ra cùng một kết quả.
/// </para>
/// <para>
/// Là hàm thuần nên kiểm được toàn bộ ma trận trạng thái bằng unit test, không cần database.
/// </para>
/// </remarks>
public static class AttemptStartPolicy
{
    /// <param name="lifecycle">Vòng đời của đề tại thời điểm gọi, tính từ giờ server.</param>
    /// <param name="attemptStatus">Trạng thái lượt đã có; NULL nghĩa là em chưa từng bắt đầu.</param>
    /// <param name="expiresAtUtc">Hạn giờ đã chốt lúc bắt đầu; chỉ có nghĩa khi đã có lượt.</param>
    /// <param name="nowUtc">Giờ server.</param>
    /// <remarks>
    /// Thứ tự xét có chủ đích: <b>lượt đã có luôn thắng vòng đời của đề</b>. Một em đã nộp bài mà
    /// nhận về câu "đề đã đóng" thì tưởng bài mình không được ghi nhận; ngược lại, giáo viên dời
    /// lịch sau khi cả lớp đã vào thi không được biến những lượt đang chạy thành lỗi.
    /// </remarks>
    public static AttemptStartDecision Decide(
        ExamLifecycle lifecycle,
        AssignmentStatus? attemptStatus,
        DateTime? expiresAtUtc,
        DateTime nowUtc) => attemptStatus switch
    {
        AssignmentStatus.Submitted or AssignmentStatus.Graded => AttemptStartDecision.AlreadyFinished,

        // Lượt bị đánh dấu hết hạn mà không có điểm: cơ hội đã đóng vĩnh viễn, không mở lại được.
        AssignmentStatus.Expired => AttemptStartDecision.Closed,

        AssignmentStatus.InProgress =>
            expiresAtUtc is null || AttemptDeadline.IsExpired(expiresAtUtc.Value, nowUtc)
                ? AttemptStartDecision.Finalise
                : AttemptStartDecision.Resume,

        // Chưa có lượt nào: lúc này mới tới lượt lịch của đề quyết định.
        _ => lifecycle switch
        {
            ExamLifecycle.Open => AttemptStartDecision.CreateNew,
            ExamLifecycle.Closed => AttemptStartDecision.Closed,
            _ => AttemptStartDecision.NotOpenYet
        }
    };
}
