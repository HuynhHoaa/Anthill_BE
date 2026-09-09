namespace EduQuiz.Domain.Enums;

/// <summary>
/// Vòng đời đề dưới góc nhìn giáo viên. Đây là giá trị <b>dẫn xuất</b>, chỉ dùng cho DTO:
/// entity Exam chỉ lưu <c>IsDraft</c>/<c>PublishedAt</c>, ba trạng thái còn lại tính theo
/// giờ server mỗi lần đọc (now &lt; OpenAt → Scheduled · trong khoảng → Open · sau CloseAt → Closed).
/// </summary>
public enum ExamLifecycle
{
    Draft,
    Scheduled,
    Open,
    Closed
}
