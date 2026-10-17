using Anthill.Application.DTOs.Exams;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;

namespace Anthill.Application.Common;

/// <summary>Một nhóm bản ghi giao bài cùng lớp, cùng cặp giờ thô và cùng trạng thái đã bắt đầu hay chưa.</summary>
/// <remarks>Repository gom sẵn trong SQL: một đề năm trăm em chỉ còn vài dòng thế này.</remarks>
public sealed record AssignmentWindowRow(
    int ExamId,
    int? GroupId,
    string? GroupName,
    DateTime? OpenAt,
    DateTime? CloseAt,
    bool Started,
    int Count,
    DateTime LatestAssignedAt);

/// <summary>
/// Dựng danh sách khung giờ theo lớp cho màn hình giáo viên từ các nhóm bản ghi đã gom.
/// </summary>
/// <remarks>
/// Hàm thuần: nhận đề và các dòng đã gom, không chạm database — nên quy tắc "khung giờ của một lớp là gì"
/// kiểm được bằng unit test, và nó dùng đúng <see cref="AssignmentSchedule.RepresentativeWindow"/> mà bước
/// giao bổ sung dùng để cho em mới vào lớp kế thừa giờ. Hai chỗ đó nói khác nhau thì giáo viên thấy một
/// giờ, em mới nhận một giờ khác.
/// </remarks>
public static class AssignmentWindowSummary
{
    public static IReadOnlyList<AssignmentWindowDto> Build(
        Exam exam,
        IEnumerable<AssignmentWindowRow> rows,
        DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(exam);
        ArgumentNullException.ThrowIfNull(rows);

        var examRows = rows.Where(row => row.ExamId == exam.Id).ToList();
        var result = new List<AssignmentWindowDto>();

        foreach (var classRows in examRows.Where(row => row.GroupId is not null).GroupBy(row => row.GroupId!.Value))
        {
            var representative = AssignmentSchedule.RepresentativeWindow(classRows.Select(ToSample));
            if (representative is not { } raw)
            {
                continue;
            }

            result.Add(ToDto(
                exam,
                classRows.Key,
                classRows.Select(row => row.GroupName).FirstOrDefault(name => name is not null),
                raw,
                classRows.Sum(row => row.Count),
                classRows.Where(row => row.Started).Sum(row => row.Count),
                asOfUtc));
        }

        // Học sinh giao lẻ (dữ liệu cũ, từ thời còn giao riêng từng em) không có "giờ của lớp" để đại diện:
        // mỗi em có thể một giờ, nên mỗi khung giờ có
        // hiệu lực là một dòng. Gom theo giờ ĐÃ PHÂN GIẢI, để em đặt giờ riêng trùng khít lịch đề không tách
        // thành một dòng trông y hệt dòng bên cạnh.
        var individuals = examRows
            .Where(row => row.GroupId is null)
            .GroupBy(row => Effective(exam, row.OpenAt, row.CloseAt));

        var individualWindows = individuals
            .Select(window => ToDto(
                exam,
                groupId: null,
                groupName: null,
                raw: window.Any(row => HasPair(row.OpenAt, row.CloseAt))
                    ? (window.Key.OpenAt, window.Key.CloseAt)
                    : (null, null),
                window.Sum(row => row.Count),
                window.Where(row => row.Started).Sum(row => row.Count),
                asOfUtc))
            .OrderBy(window => window.OpenAt)
            .ThenBy(window => window.CloseAt);

        return result
            .OrderBy(window => window.GroupName, StringComparer.CurrentCulture)
            .Concat(individualWindows)
            .ToList();
    }

    private static AssignmentSchedule.WindowSample ToSample(AssignmentWindowRow row) =>
        new(row.OpenAt, row.CloseAt, row.Started, row.Count, row.LatestAssignedAt);

    private static bool HasPair(DateTime? openAt, DateTime? closeAt) => openAt.HasValue && closeAt.HasValue;

    private static (DateTime OpenAt, DateTime CloseAt) Effective(Exam exam, DateTime? openAt, DateTime? closeAt) =>
        HasPair(openAt, closeAt)
            ? (openAt!.Value.ToUtc(), closeAt!.Value.ToUtc())
            : (exam.OpenAt.ToUtc(), exam.CloseAt.ToUtc());

    private static AssignmentWindowDto ToDto(
        Exam exam,
        int? groupId,
        string? groupName,
        (DateTime? OpenAt, DateTime? CloseAt) raw,
        int assignedCount,
        int startedCount,
        DateTime asOfUtc)
    {
        var (openAt, closeAt) = Effective(exam, raw.OpenAt, raw.CloseAt);

        return new AssignmentWindowDto
        {
            GroupId = groupId,
            GroupName = groupName,
            OpenAt = openAt,
            CloseAt = closeAt,
            HasScheduleOverride = HasPair(raw.OpenAt, raw.CloseAt),
            Lifecycle = ExamMappingExtensions.ResolveWindowLifecycle(exam.IsDraft, openAt, closeAt, asOfUtc)
                .ToLifecycleString(),
            ScheduleLocked = AssignmentSchedule.IsScheduleLocked(openAt, closeAt, asOfUtc),
            AssignedCount = assignedCount,
            StartedCount = startedCount
        };
    }
}
