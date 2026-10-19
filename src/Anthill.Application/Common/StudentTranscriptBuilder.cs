using Anthill.Application.DTOs.ExamAttempts;
using Anthill.Application.DTOs.MyLearning;

namespace Anthill.Application.Common;

/// <summary>
/// Dựng bảng điểm của một học sinh từ các bài đã giao cho em. Hàm thuần, không đụng database nên kiểm bằng unit test được.
/// </summary>
/// <remarks>
/// <para>
/// <b>Điểm trung bình môn</b> = trung bình các bài ĐÃ ĐẾN HẠN TÍNH: bài đã nộp và được xem điểm (điểm quy về thang 10), cộng bài hết hạn mà
/// em không làm (0 điểm, giống bảng điểm giáo viên xuất ra Excel). Bài còn làm được, bài chưa mở và bài đã nộp nhưng chưa được xem điểm
/// <b>không</b> tính: tính 0 cho bài em còn đang có thể làm là phạt oan, còn bài giấu điểm thì không có con số nào để tính (và đưa vào
/// trung bình cũng là lộ điểm qua đường vòng).
/// </para>
/// <para>
/// <b>Môn</b> của một bài là môn của giáo viên ra đề. Đổi giáo viên phụ trách giữa chừng thì bài cũ vẫn thuộc môn cũ.
/// </para>
/// </remarks>
public static class StudentTranscriptBuilder
{
    public const string UnassignedSubjectName = "Chưa gán môn";

    public static TranscriptDto Build(IReadOnlyList<(StudentExamItemDto Item, string? SubjectName)> exams)
    {
        ArgumentNullException.ThrowIfNull(exams);

        var subjects = exams
            .GroupBy(entry => string.IsNullOrWhiteSpace(entry.SubjectName) ? UnassignedSubjectName : entry.SubjectName.Trim())
            .Select(group => BuildSubject(group.Key, group.Select(entry => entry.Item).ToList()))
            .OrderBy(subject => subject.SubjectName == UnassignedSubjectName ? 1 : 0)
            .ThenBy(subject => subject.SubjectName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var averages = subjects.Where(subject => subject.Average is not null).Select(subject => subject.Average!.Value).ToList();

        return new TranscriptDto
        {
            OverallAverage = averages.Count == 0 ? null : Round(averages.Average()),
            Subjects = subjects
        };
    }

    private static TranscriptSubjectDto BuildSubject(string subjectName, IReadOnlyList<StudentExamItemDto> items)
    {
        var rows = items.Select(Classify).ToList();

        var counted = rows.Where(row => row.Counted).Select(row => row.Value ?? 0m).ToList();

        return new TranscriptSubjectDto
        {
            SubjectName = subjectName,
            GroupNames = items
                .Select(item => item.GroupName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct()
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            Average = counted.Count == 0 ? null : Round(counted.Average()),
            CountedExams = counted.Count,
            TotalExams = items.Count,
            HiddenExams = rows.Count(row => row.Kind == Kind.Hidden),
            PendingExams = rows.Count(row => row.Kind == Kind.Pending),
            UpcomingExams = rows.Count(row => row.Kind == Kind.Upcoming),
            MissedExams = rows.Count(row => row.Kind == Kind.Missed),
            Exams = rows
                .Select(row => new TranscriptExamDto
                {
                    ExamId = row.Item.ExamId,
                    AssignmentId = row.Item.AssignmentId,
                    Title = row.Item.Title,
                    GroupId = row.Item.GroupId,
                    GroupName = row.Item.GroupName,
                    Status = row.Item.Status,
                    Score = row.Kind == Kind.Scored ? row.Value : null,
                    Counted = row.Counted,
                    AttemptId = row.Item.AttemptId,
                    SubmittedAt = row.Item.SubmittedAt,
                    CloseAt = row.Item.CloseAt
                })
                .ToList()
        };
    }

    private enum Kind
    {
        Scored,
        Hidden,
        Missed,
        Pending,
        Upcoming
    }

    private sealed record Row(StudentExamItemDto Item, Kind Kind, decimal? Value)
    {
        public bool Counted => Kind is Kind.Scored or Kind.Missed;
    }

    private static Row Classify(StudentExamItemDto item)
    {
        switch (item.Status)
        {
            case "graded":
            case "submitted":
                return item.Score is { } score && item.MaxScore is { } maxScore && ExamScoring.ToStandardScale(score, maxScore) is { } scaled
                    ? new Row(item, Kind.Scored, Round(scaled))
                    : new Row(item, Kind.Hidden, null);

            case "expired":
                return new Row(item, Kind.Missed, 0m);

            default:
                // assigned / inprogress: chưa tới mở thì là "sắp tới", còn lại là việc em còn làm được.
                return item.Lifecycle == "scheduled"
                    ? new Row(item, Kind.Upcoming, null)
                    : new Row(item, Kind.Pending, null);
        }
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
