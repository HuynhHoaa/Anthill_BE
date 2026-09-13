using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>
/// Khi nào một em chưa bắt đầu làm bài đáng được nhắc rằng giờ của em sắp hết.
/// </summary>
/// <remarks>
/// <para>
/// Hàm thuần, không chạm database và không đọc đồng hồ — giờ được đưa vào — nên quy tắc nhắc kiểm được
/// bằng unit test. Đây là loại lỗi không lộ ra ở môi trường thử: nhắc quá sớm thì em bỏ qua, nhắc quá
/// muộn thì em không còn đủ thời gian làm bài, và cả hai đều chỉ thấy được vào đúng hôm thi.
/// </para>
/// <para>
/// Mốc nhắc tính lùi từ giờ đóng một khoảng bằng <b>thời lượng làm bài cộng thêm một biên</b>, chứ không
/// phải một con số cố định: một bài 15 phút và một bài 3 tiếng không thể cùng được nhắc trước nửa tiếng.
/// Nhắc vào đúng lúc "trễ hơn nữa là không còn đủ giờ" mới là lời nhắc có ích.
/// </para>
/// </remarks>
public static class DeadlineReminder
{
    /// <summary>Có nhắc hay không, và còn bao nhiêu phút nữa thì đóng.</summary>
    public readonly record struct Decision(bool ShouldSend, int MinutesRemaining)
    {
        public static Decision No => new(false, 0);
    }

    /// <summary>Khoảng thời gian trước giờ đóng mà lời nhắc được gửi.</summary>
    public static TimeSpan LeadTime(int durationMinutes, int marginMinutes) =>
        TimeSpan.FromMinutes(durationMinutes + marginMinutes);

    /// <param name="openAt">Giờ mở có hiệu lực của em (<see cref="AssignmentSchedule.Resolve(ExamAssignment)"/>).</param>
    /// <param name="closeAt">Giờ đóng có hiệu lực của em.</param>
    /// <param name="durationMinutes">Thời lượng làm bài của đề.</param>
    /// <param name="marginMinutes">Biên cộng thêm vào thời lượng để ra mốc nhắc.</param>
    /// <param name="alreadyNotifiedCloseAt">Mốc đóng mà em đã được nhắc; NULL khi chưa nhắc lần nào.</param>
    /// <param name="now">Giờ server của lượt quét.</param>
    public static Decision Decide(
        DateTime openAt,
        DateTime closeAt,
        int durationMinutes,
        int marginMinutes,
        DateTime? alreadyNotifiedCloseAt,
        DateTime now)
    {
        var open = openAt.ToUtc();
        var close = closeAt.ToUtc();
        var moment = now.ToUtc();

        // Bài đã đóng thì lời nhắc chỉ còn là lời trách; bài chưa mở thì "sắp hết giờ" là câu vô nghĩa.
        if (close <= moment || open > moment)
        {
            return Decision.No;
        }

        var lead = LeadTime(durationMinutes, marginMinutes);

        // Khung giờ vừa khít một tiết học — mở lúc vào lớp, đóng lúc hết tiết — thì mốc nhắc rơi vào trước
        // cả giờ mở, tức em vừa nhận bài đã bị báo sắp hết giờ. Buổi đó có giáo viên ngồi ngay đó nhắc rồi.
        if (close - open <= lead)
        {
            return Decision.No;
        }

        if (close - moment > lead)
        {
            return Decision.No;
        }

        // Đã nhắc cho đúng khung này rồi. So theo mốc đóng chứ không theo một cờ: giáo viên dời khung sang
        // giờ khác là một khung khác, và em phải được nhắc lại theo giờ mới.
        if (alreadyNotifiedCloseAt?.ToUtc() == close)
        {
            return Decision.No;
        }

        return new Decision(true, (int)Math.Ceiling((close - moment).TotalMinutes));
    }
}

/// <summary>Nội dung lời nhắc sắp hết giờ.</summary>
/// <remarks>
/// Cùng lý do với <see cref="ScheduleNoticeText"/>: không ghi giờ cụ thể, vì server chạy theo UTC và không
/// biết trường ở múi giờ nào. Nói "còn khoảng bao nhiêu phút" thì đúng ở mọi múi giờ, và đó cũng là con số
/// em cần để quyết định có vào thi ngay hay không.
/// </remarks>
public static class DeadlineReminderText
{
    public static Notification For(ExamAssignment assignment, Exam exam, int minutesRemaining, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(exam);

        var tail = minutesRemaining >= exam.DurationMinutes
            ? "Hãy vào làm trước khi hết giờ."
            : $"Vào muộn hơn nữa thì bạn không còn đủ {exam.DurationMinutes} phút làm bài.";

        return new Notification
        {
            RecipientUserId = assignment.StudentId,
            Type = NotificationType.DeadlineSoon,
            Title = "Bài kiểm tra sắp đóng",
            Message = $"Bài \"{exam.Title}\" còn khoảng {minutesRemaining} phút nữa là đóng "
                      + $"mà bạn chưa bắt đầu làm. {tail}",
            Link = ScheduleNoticeText.StudentExamLink(assignment.Id),
            TargetType = nameof(ExamAssignment),
            TargetId = assignment.Id,
            IsRead = false,
            CreatedAt = now
        };
    }
}
