using System.Globalization;
using System.Text;
using Anthill.Application.Common;
using Anthill.Application.DTOs.Files;
using Anthill.Application.Interfaces.Files;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

/// <summary>
/// Cài đặt <see cref="IGradeSheetService"/>: dựng bảng điểm từ dữ liệu đã có rồi nhờ <see cref="ISpreadsheetService"/> ghi ra .xlsx.
/// </summary>
/// <remarks>
/// <para>
/// <b>Điểm trong file luôn quy về thang 10</b> (cùng phép chia với mọi con số tổng hợp khác trong hệ thống): hai đề có điểm tối đa
/// khác nhau cạnh nhau trong một bảng thì cộng trung bình điểm thô không có nghĩa.
/// </para>
/// <para>
/// <b>Bài được giao mà em không làm là 0 điểm</b> (quy tắc của người dùng, 19/10): ô ghi 0 và vẫn tính vào trung bình. Bản đầu để ô
/// trống và bỏ ô đó khỏi trung bình, nên một em bỏ cả đề vẫn có điểm trung bình bằng điểm của đề còn lại. Ngoại lệ duy nhất là em vào
/// lớp <b>sau</b> khi bài được giao: em không có bài đó nên ô ghi dấu — và không tính vào trung bình.
/// </para>
/// </remarks>
public sealed class GradeSheetService : IGradeSheetService
{
    /// <summary>Việt Nam không có giờ mùa hè nên cộng thẳng 7 giờ là đủ; máy chủ chạy UTC.</summary>
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private const string NotPublishedScore = "Chưa công bố";
    private const string NotAssigned = "—";
    private const string GroupNotFoundMessage = "Không tìm thấy lớp này.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ISpreadsheetService _spreadsheets;

    public GradeSheetService(IUnitOfWork unitOfWork, ICurrentUser currentUser, ISpreadsheetService spreadsheets)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _spreadsheets = spreadsheets;
    }

    /// <inheritdoc />
    public async Task<Result<ExportedFileDto>> ExportMyResultsAsync(int? termId = null, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(UserRole.Student))
        {
            return Result.Forbidden<ExportedFileDto>("Chỉ học sinh mới xuất được bảng điểm của mình.");
        }

        var studentId = _currentUser.RequireUserId();
        var student = await _unitOfWork.Users.GetByIdAsync(studentId, cancellationToken);
        if (student is null)
        {
            return Result.NotFound<ExportedFileDto>("Không tìm thấy tài khoản.");
        }

        var attempts = await _unitOfWork.ExamAttempts.GetFinishedByStudentAsync(studentId, cancellationToken);

        // Lọc theo kỳ như bảng điểm trên màn hình: bài của lớp chưa phân kỳ ứng với kỳ 0.
        string? termName = null;
        if (termId is { } wantedTerm)
        {
            attempts = attempts.Where(attempt => (attempt.Assignment.AssignedViaGroup?.AcademicTermId ?? 0) == wantedTerm).ToList();
            termName = wantedTerm == 0
                ? "Chưa phân kỳ"
                : (await _unitOfWork.AcademicTerms.GetByIdAsync(wantedTerm, cancellationToken))?.Name;
        }

        var rows = new List<IReadOnlyList<object?>>(attempts.Count);
        var revealedScores = new List<decimal>();

        foreach (var attempt in attempts)
        {
            var exam = attempt.Assignment.Exam;
            var revealed = attempt.CanRevealScore(isOwningTeacher: false);
            var scaled = revealed ? ExamScoring.ToStandardScale(attempt.Score, attempt.MaxScore) : null;
            if (scaled is { } value)
            {
                revealedScores.Add(Round(value));
            }

            rows.Add(
            [
                rows.Count + 1,
                exam.Title,
                exam.Topic ?? string.Empty,
                attempt.Assignment.AssignedViaGroup?.Name ?? string.Empty,
                attempt.SubmittedAt is { } submittedAt ? FormatLocal(submittedAt) : string.Empty,
                revealed ? attempt.CorrectCount : null,
                revealed ? Round(attempt.Score) : NotPublishedScore,
                revealed ? Round(attempt.MaxScore) : null,
                scaled is { } standard ? Round(standard) : null,
                scaled is { } graded ? Classify(graded) : string.Empty
            ]);
        }

        var footer = new List<IReadOnlyList<object?>>();
        if (revealedScores.Count > 0)
        {
            footer.Add(
            [
                null, "Điểm trung bình (thang 10)", null, null, null, null, null, null,
                Round(revealedScores.Average()), null
            ]);
        }

        var hidden = attempts.Count - revealedScores.Count;
        var notes = new List<string>
        {
            "BẢNG ĐIỂM CÁ NHÂN",
            $"Học sinh: {student.FullName} · Mã: {student.Code}",
            $"Xuất lúc {FormatLocal(DateTime.UtcNow)}. Điểm quy về thang 10."
        };
        if (termName is not null)
        {
            notes.Insert(2, $"Kỳ học: {termName}");
        }
        if (hidden > 0)
        {
            notes.Add($"{hidden} bài chưa được giáo viên công bố điểm nên chưa tính vào trung bình.");
        }

        var table = new SpreadsheetTable(
            "Bảng điểm",
            notes,
            [
                new TableColumn("STT", 6, Centered: true),
                new TableColumn("Bài kiểm tra", 42),
                new TableColumn("Chủ đề", 24),
                new TableColumn("Lớp", 22),
                new TableColumn("Nộp lúc", 18),
                new TableColumn("Số câu đúng", 12, Centered: true),
                new TableColumn("Điểm", 12, Centered: true),
                new TableColumn("Điểm tối đa", 12, Centered: true),
                new TableColumn("Điểm thang 10", 14, Centered: true),
                new TableColumn("Xếp loại", 14, Centered: true)
            ],
            rows,
            footer);

        return Result.Success(new ExportedFileDto(
            $"bang-diem-{Slug(student.Code ?? string.Empty, 24)}.xlsx",
            _spreadsheets.BuildTable(table)));
    }

    /// <inheritdoc />
    public async Task<Result<ExportedFileDto>> ExportGroupAsync(int groupId, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(UserRole.Teacher))
        {
            return Result.Forbidden<ExportedFileDto>("Chỉ giáo viên mới xuất được bảng điểm lớp.");
        }

        var teacherId = _currentUser.RequireUserId();

        // 404 cho cả lớp không có, lớp còn là bản nháp lẫn lớp của đồng nghiệp: 403 sẽ xác nhận id đó có thật.
        if (!await _unitOfWork.StudentGroups.IsTeachingAsync(groupId, teacherId, cancellationToken))
        {
            return Result.NotFound<ExportedFileDto>(GroupNotFoundMessage);
        }

        var group = await _unitOfWork.StudentGroups.GetByIdAsync(groupId, cancellationToken);
        var teacher = await _unitOfWork.Users.GetByIdAsync(teacherId, cancellationToken);
        if (group is null || teacher is null)
        {
            return Result.NotFound<ExportedFileDto>(GroupNotFoundMessage);
        }

        var students = (await _unitOfWork.Users.GetStudentsByGroupAsync(groupId, cancellationToken))
            .Where(student => student.Status != UserStatus.Deleted)
            .ToList();

        // Cột là mọi đề đã giao cho lớp này, của BẤT KỲ giáo viên nào, theo thứ tự soạn đề. Giáo viên phụ trách một lớp chứ không phải
        // một đề: lớp đổi giáo viên giữa chừng là chuyện có thật, và người mới cần thấy điểm các đề người cũ đã giao để nắm sức học của
        // cả lớp. (Một em không học hai lớp cùng môn nên một đề không bị giao qua hai lớp mà em học cả hai.)
        var assignments = await _unitOfWork.ExamAssignments.GetByGroupAsync(groupId, cancellationToken);

        var exams = assignments
            .Select(assignment => assignment.Exam)
            .DistinctBy(exam => exam.Id)
            .OrderBy(exam => exam.Id)
            .ToList();

        var cell = assignments.ToDictionary(assignment => (assignment.ExamId, assignment.StudentId));

        var columns = new List<TableColumn>
        {
            new("STT", 6, Centered: true),
            new("Mã học sinh", 14),
            new("Họ và tên", 28)
        };
        columns.AddRange(exams.Select(exam => new TableColumn(exam.Title, 16, Centered: true)));
        columns.Add(new TableColumn("Điểm trung bình", 16, Centered: true));

        var examScores = exams.ToDictionary(exam => exam.Id, _ => new List<decimal>());
        var studentAverages = new List<decimal>();
        var rows = new List<IReadOnlyList<object?>>(students.Count);

        foreach (var student in students)
        {
            var row = new List<object?> { rows.Count + 1, student.Code, student.FullName };
            var scores = new List<decimal>();

            foreach (var exam in exams)
            {
                // Không có dòng giao bài: em vào lớp sau khi bài được giao nên không có bài này — không tính vào trung bình.
                if (!cell.TryGetValue((exam.Id, student.Id), out var assignment))
                {
                    row.Add(NotAssigned);
                    continue;
                }

                // Có bài mà chưa nộp (chưa vào làm, đang làm dở hay hết giờ không nộp) thì không có điểm nào để tính: 0 điểm.
                var score = assignment.Attempt is { Status: AssignmentStatus.Graded or AssignmentStatus.Submitted } attempt
                    && ExamScoring.ToStandardScale(attempt.Score, attempt.MaxScore) is { } scaled
                    ? Round(scaled)
                    : 0m;

                row.Add(score);
                scores.Add(score);
                examScores[exam.Id].Add(score);
            }

            if (scores.Count > 0)
            {
                var average = Round(scores.Average());
                row.Add(average);
                studentAverages.Add(average);
            }
            else
            {
                row.Add(null);
            }

            rows.Add(row);
        }

        var footer = new List<IReadOnlyList<object?>>();
        if (exams.Count > 0 && students.Count > 0)
        {
            var averageRow = new List<object?> { null, null, "Trung bình lớp" };
            foreach (var exam in exams)
            {
                var scores = examScores[exam.Id];
                averageRow.Add(scores.Count > 0 ? Round(scores.Average()) : null);
            }

            averageRow.Add(studentAverages.Count > 0 ? Round(studentAverages.Average()) : null);

            footer.Add(averageRow);
        }

        var notes = new List<string>
        {
            $"BẢNG ĐIỂM LỚP {group.Name}",
            $"Giáo viên: {teacher.FullName} · Sĩ số: {students.Count}",
            $"Xuất lúc {FormatLocal(DateTime.UtcNow)}. Điểm quy về thang 10; bài chưa làm tính 0 điểm; dấu — là em vào lớp sau khi bài được giao nên không tính."
        };
        if (exams.Count == 0)
        {
            notes.Add("Bạn chưa giao bài kiểm tra nào cho học sinh của lớp này.");
        }

        var table = new SpreadsheetTable("Bảng điểm", notes, columns, rows, footer, FrozenColumns: 3);

        return Result.Success(new ExportedFileDto(
            $"bang-diem-lop-{Slug(group.Name, 32)}.xlsx",
            _spreadsheets.BuildTable(table)));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Xếp loại theo tỷ lệ so với điểm tối đa, cùng mốc với giao diện: Giỏi từ 80%, Khá từ 65%, Trung bình từ 50%.</summary>
    private static string Classify(decimal scoreOnStandardScale)
    {
        var ratio = scoreOnStandardScale / ExamScoring.TotalScore;

        return ratio switch
        {
            >= 0.8m => "Giỏi",
            >= 0.65m => "Khá",
            >= 0.5m => "Trung bình",
            _ => "Yếu"
        };
    }

    private static string FormatLocal(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(VietnamOffset).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Tên file chỉ chữ ASCII thường, số và dấu gạch: bỏ dấu tiếng Việt, ký tự lạ thành dấu gạch, cắt cho ngắn.</summary>
    private static string Slug(string value, int maxLength)
    {
        var builder = new StringBuilder();

        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character == 'đ' || character == 'Đ' ? 'd' : character);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(lower);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].Trim('-');
        }

        return slug.Length == 0 ? "lop" : slug;
    }
}
