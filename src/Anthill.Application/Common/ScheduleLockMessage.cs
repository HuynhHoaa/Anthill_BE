using Anthill.Application.Mappings;

namespace Anthill.Application.Common;

/// <summary>Câu báo khi một lần đổi giờ bị chặn vì khung giờ đang khoá (<see cref="AssignmentSchedule.IsScheduleLocked"/>).</summary>
public static class ScheduleLockMessage
{
    public const string ExamSubject = "Lịch chung của đề";
    public const string StudentSubject = "Bài của em này";

    /// <summary>Tên lớp đã mở đầu bằng "Lớp" thì không thêm chữ "lớp" nữa, tránh "lớp Lớp 10A2".</summary>
    public static string ClassSubject(string className) =>
        className.TrimStart().StartsWith("Lớp ", StringComparison.OrdinalIgnoreCase)
            ? $"Bài của {className.Trim()}"
            : $"Bài của lớp {className.Trim()}";

    /// <param name="subject">Cái đang bị chặn, đứng đầu câu: "Đề này", "Bài của lớp 12A1"...</param>
    /// <param name="openAt">Giờ mở của khung giờ đang khoá.</param>
    /// <param name="now">Giờ server của lần đổi.</param>
    public static string For(string subject, DateTime openAt, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        return now.ToUtc() < openAt.ToUtc()
            ? $"{subject} còn chưa đầy {LeadText()} nữa là mở nên không đổi giờ được."
            : $"{subject} đang mở nên không đổi giờ được.";
    }

    private static string LeadText()
    {
        var minutes = AssignmentSchedule.ScheduleLockLeadMinutes;

        return minutes % 60 == 0 ? $"{minutes / 60} giờ" : $"{minutes} phút";
    }
}
