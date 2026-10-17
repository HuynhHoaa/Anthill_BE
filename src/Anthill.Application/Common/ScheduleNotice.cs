using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>Một em cần được báo rằng lịch làm bài của em vừa đổi, và đổi ở chỗ nào.</summary>
/// <remarks>
/// Bốn đường làm dời giờ của một em đi qua đúng hai hàm dựng dưới đây, nên câu hỏi "em nào đáng được báo"
/// chỉ được trả lời ở một chỗ. Mỗi đường tự quyết thì sớm muộn một đường báo cho cả lớp không bị ảnh hưởng,
/// hay lặng thinh với lớp vừa bị dời.
/// </remarks>
public sealed record ScheduleNotice(ExamAssignment Assignment, bool WindowChanged, bool DurationChanged)
{
    /// <summary>Có gì để báo hay không.</summary>
    public bool HasChange => WindowChanged || DurationChanged;

    /// <summary>Một lần sửa đề (PUT hoặc PATCH lịch) đổi gì với một em chưa bắt đầu làm bài.</summary>
    public static ScheduleNotice FromExamEdit(
        ExamAssignment assignment,
        AssignmentSchedule.ExamTiming before,
        Exam after,
        DateTime now)
    {
        var (windowChanged, durationChanged) = AssignmentSchedule.ChangesFromExamEdit(assignment, before, after, now);
        return new ScheduleNotice(assignment, windowChanged, durationChanged);
    }

    /// <summary>Một lần dời khung giờ của chính lượt giao — gửi lại lớp kèm giờ.</summary>
    /// <remarks>
    /// Đặt giờ riêng trùng khít lịch đề, hay gỡ giờ riêng vốn trùng lịch đề, không dời em đi phút nào nên
    /// không sinh thông báo. Thời lượng không đổi ở đường này.
    /// </remarks>
    public static ScheduleNotice FromReschedule(
        AssignmentSchedule.RescheduleChange change,
        Exam exam,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(exam);

        var closeBefore = change.PreviousOpenAt.HasValue && change.PreviousCloseAt.HasValue
            ? change.PreviousCloseAt.Value
            : exam.CloseAt;
        var (_, closeAt) = AssignmentSchedule.Resolve(change.Assignment, exam);

        var windowChanged = !exam.IsDraft
                            && AssignmentSchedule.EffectiveWindowChanged(
                                change.Assignment,
                                exam,
                                change.PreviousOpenAt,
                                change.PreviousCloseAt)
                            && AssignmentSchedule.StillMatters(closeBefore, closeAt, now);

        return new ScheduleNotice(change.Assignment, windowChanged, DurationChanged: false);
    }
}

/// <summary>Nội dung thông báo về giờ làm bài gửi cho học sinh.</summary>
/// <remarks>
/// <para>
/// Không ghi giờ cụ thể vào câu chữ. Server chạy theo UTC và không biết trường đặt ở múi giờ nào; một câu
/// "mở lúc 01:00" là sai giờ với mọi học sinh ở Việt Nam. Thông báo dẫn thẳng tới trang giới thiệu bài, nơi
/// trình duyệt của em hiện giờ mở và hạn nộp theo giờ máy.
/// </para>
/// <para>Hàm thuần để câu chữ theo từng trường hợp kiểm được bằng unit test.</para>
/// </remarks>
public static class ScheduleNoticeText
{
    /// <summary>Đường dẫn tới trang giới thiệu bài của chính em — nơi hiện giờ theo múi giờ của em.</summary>
    public static string StudentExamLink(int assignmentId) => $"/hoc-sinh/bai-kiem-tra/{assignmentId}";

    public static Notification ExamUpdated(ScheduleNotice notice, Exam exam, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(notice);
        ArgumentNullException.ThrowIfNull(exam);

        var message = (notice.WindowChanged, notice.DurationChanged) switch
        {
            (true, true) =>
                $"Giáo viên đã đổi giờ làm bài \"{exam.Title}\" và thời gian làm bài nay là {exam.DurationMinutes} phút. "
                + "Hãy mở bài để xem giờ mở và hạn nộp mới của bạn.",
            (false, true) =>
                $"Thời gian làm bài \"{exam.Title}\" đã đổi thành {exam.DurationMinutes} phút.",
            _ =>
                $"Giáo viên đã đổi giờ làm bài \"{exam.Title}\". Hãy mở bài để xem giờ mở và hạn nộp mới của bạn."
        };

        return new Notification
        {
            RecipientUserId = notice.Assignment.StudentId,
            Type = NotificationType.ExamUpdated,
            Title = "Lịch làm bài đã thay đổi",
            Message = message,
            Link = StudentExamLink(notice.Assignment.Id),
            TargetType = nameof(ExamAssignment),
            TargetId = notice.Assignment.Id,
            IsRead = false,
            CreatedAt = now
        };
    }
}
