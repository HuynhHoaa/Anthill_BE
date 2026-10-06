using Anthill.Application.Mappings;

namespace Anthill.Application.Common;

/// <summary>Câu báo khi một lần đổi giờ bị chặn vì khung giờ đang khoá (<see cref="AssignmentSchedule.IsScheduleLocked"/>).</summary>
public static class ScheduleLockMessage
{
    public const string ExamSubject = "Đề này";
    public const string StudentSubject = "Bài của em này";

    public static string ClassSubject(string className) => $"Bài của lớp {className}";

    /// <param name="subject">Cái đang bị chặn, đứng đầu câu: "Đề này", "Bài của lớp 12A1"...</param>
    /// <param name="openAt">Giờ mở của khung giờ đang khoá.</param>
    /// <param name="now">Giờ server của lần đổi.</param>
    public static string For(string subject, DateTime openAt, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        return now.ToUtc() < openAt.ToUtc()
            ? $"{subject} sắp mở trong chưa đầy {AssignmentSchedule.ScheduleLockLeadMinutes / 60} giờ nữa nên không đổi giờ được."
            : $"{subject} đang mở nên không đổi giờ được.";
    }
}
