using Anthill.Application.Common;
using Anthill.Application.DTOs.ExamAssignments;
using Anthill.Application.Interfaces.Realtime;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Mappings;

/// <summary>
/// Chuyển <see cref="ExamAssignment"/> sang DTO, và một chỗ duy nhất định nghĩa "tiến trình làm
/// bài của một học sinh là gì".
/// </summary>
public static class ExamAssignmentMappingExtensions
{
    /// <summary>
    /// Suy trạng thái từ lượt làm bài và lịch CÓ HIỆU LỰC của bản ghi giao bài.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Không có cột nào lưu giá trị này. Đã có lượt làm bài thì trạng thái của lượt đó là câu trả
    /// lời; chưa có thì còn tuỳ khung giờ của em đã đóng hay chưa.
    /// </para>
    /// <para>
    /// Nhánh <c>Expired</c> là chỗ đáng nói: một em chưa từng bắt đầu, sau khi khung giờ của em đóng
    /// — lịch riêng của lớp nếu có, ngược lại lịch chung của đề — nếu vẫn báo <c>assigned</c> thì bảng
    /// theo dõi đọc như thể em còn cơ hội làm bài. Giáo viên cần phân biệt "chưa làm nhưng còn kịp"
    /// với "hết giờ mà không vào thi" — đó là hai tình huống dẫn tới hai hành động khác nhau.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Chưa có lượt làm bài mà Exam chưa được nạp.</exception>
    public static AssignmentStatus ResolveStatus(this ExamAssignment assignment, DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (assignment.Attempt is not null)
        {
            return assignment.Attempt.Status;
        }

        // Hết hạn theo khung giờ CỦA EM NÀY — cùng một phép tính với Lifecycle nằm ngay cạnh trên
        // cùng DTO. Đọc exam.CloseAt ở đây là để hai trường đó nói hai điều ngược nhau: lớp chiều
        // đang trong giờ lại báo "expired" vì lịch chung đã đóng, còn lớp sáng lỡ giờ vẫn báo
        // "assigned" vì lịch chung còn mở. Nháp không bao giờ ra Closed nên nhánh nháp tự đúng theo.
        return assignment.ResolveLifecycle(asOfUtc) == ExamLifecycle.Closed
            ? AssignmentStatus.Expired
            : AssignmentStatus.Assigned;
    }

    /// <remarks>
    /// <paramref name="assignment"/> phải được nạp kèm <c>Exam</c> (repository của bảng theo dõi đã
    /// Include): cả trạng thái lẫn khung giờ đều cần lịch chung của đề làm mặc định.
    /// </remarks>
    public static ExamAssignmentDto ToDto(this ExamAssignment assignment, DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var attempt = assignment.Attempt;
        var status = assignment.ResolveStatus(asOfUtc);
        var (openAt, closeAt) = AssignmentSchedule.Resolve(assignment);

        return new ExamAssignmentDto
        {
            Id = assignment.Id,
            ExamId = assignment.ExamId,
            StudentId = assignment.StudentId,
            StudentName = assignment.Student?.FullName ?? string.Empty,
            StudentCode = assignment.Student?.Code ?? string.Empty,

            // Repository luôn nạp kèm Student; vế dự phòng chỉ để dựng DTO được khi navigation vắng,
            // cùng kiểu với hai trường tên và mã ngay trên — không có tài khoản thì không có gì để gắn nhãn.
            StudentStatus = (assignment.Student?.Status ?? UserStatus.Active).ToStatusString(),
            Group = assignment.Student?.Group?.Name ?? string.Empty,
            Status = status.ToAssignmentStatusString(),
            AttemptId = attempt?.Id,

            // Điểm chỉ có nghĩa sau khi đã chấm. Trả 0 cho một bài đang làm dở thì màn hình kết quả
            // hiện "0 điểm" y hệt một bài làm sai hết.
            Score = attempt is { Status: AssignmentStatus.Graded } ? attempt.Score : null,

            StartedAt = attempt?.StartedAt.ToUtc(),
            SubmittedAt = attempt?.SubmittedAt.ToUtc(),
            AssignedAt = assignment.AssignedAt.ToUtc(),
            OpenAt = openAt.ToUtc(),
            CloseAt = closeAt.ToUtc(),
            HasScheduleOverride = AssignmentSchedule.HasOverride(assignment),
            ScheduleLocked = AssignmentSchedule.IsScheduleLocked(openAt, closeAt, asOfUtc),
            AssignedViaGroupId = assignment.AssignedViaGroupId
        };
    }

    /// <summary>Sự kiện realtime báo cho MỘT em rằng em vừa được giao bài.</summary>
    /// <remarks>
    /// Giờ mở/đóng là giờ của chính bản ghi này — lịch riêng của lớp nếu có, ngược lại mới là lịch
    /// chung — tức đúng hai mốc mà <c>GET /api/my/exams</c> trả về. Lấy lịch của đề thì thông báo hứa
    /// "còn một tuần" trong khi thẻ bài của em đếm ngược ba tiếng.
    /// </remarks>
    public static ExamAssignedEvent ToAssignedEvent(this ExamAssignment assignment, Exam exam, string teacherName)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(exam);

        var (openAt, closeAt) = AssignmentSchedule.Resolve(assignment, exam);

        return new ExamAssignedEvent
        {
            ExamId = exam.Id,
            AssignmentId = assignment.Id,
            ExamTitle = exam.Title,
            TeacherName = teacherName,
            DurationMinutes = exam.DurationMinutes,
            OpenAt = openAt.ToUtc(),
            CloseAt = closeAt.ToUtc()
        };
    }

    /// <summary>Sự kiện realtime báo cho MỘT em rằng đề em được giao vừa đổi lịch hoặc thời lượng.</summary>
    /// <remarks>
    /// Dựng theo từng bản ghi vì mỗi em có thể theo một khung giờ khác. Sửa lịch chung của đề chỉ dời
    /// những em KHÔNG có giờ riêng; gửi một payload mang lịch đề cho cả danh sách là báo cho lớp có giờ
    /// riêng rằng giờ của họ đã đổi trong khi thực tế không đổi. Người gọi dùng
    /// <see cref="AssignmentSchedule.HasOverride"/> để bỏ qua những em không bị ảnh hưởng.
    /// </remarks>
    public static ExamUpdatedEvent ToUpdatedEvent(this ExamAssignment assignment, Exam exam)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(exam);

        var (openAt, closeAt) = AssignmentSchedule.Resolve(assignment, exam);

        return new ExamUpdatedEvent
        {
            ExamId = exam.Id,
            AssignmentId = assignment.Id,
            ExamTitle = exam.Title,
            DurationMinutes = exam.DurationMinutes,
            OpenAt = openAt.ToUtc(),
            CloseAt = closeAt.ToUtc()
        };
    }

    /// <summary>Chuỗi trạng thái viết thường, đúng như kiểu <c>ExamStatus</c> của frontend khai báo.</summary>
    public static string ToAssignmentStatusString(this AssignmentStatus status) => status switch
    {
        AssignmentStatus.Assigned => "assigned",
        AssignmentStatus.InProgress => "inprogress",
        AssignmentStatus.Submitted => "submitted",
        AssignmentStatus.Graded => "graded",
        AssignmentStatus.Expired => "expired",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
