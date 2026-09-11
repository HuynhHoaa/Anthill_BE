namespace Anthill.Domain.Enums;

/// <summary>
/// Tiến trình làm bài của một học sinh với một đề. <c>Assigned</c> là trạng thái dẫn xuất
/// khi assignment chưa có attempt nào; các giá trị còn lại được lưu ở <c>ExamAttempt.Status</c>.
/// </summary>
public enum AssignmentStatus
{
    Assigned,
    InProgress,
    Submitted,
    Graded,
    Expired
}
