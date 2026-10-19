using Anthill.Application.Common;
using Anthill.Application.DTOs.Imports;
using Anthill.Application.Interfaces.Files;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Validators;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

/// <summary>
/// Nhập lớp từ file Excel: đầu năm học nhà trường lập cả chục đến cả trăm lớp, mỗi lớp kèm giáo viên phụ trách.
/// </summary>
/// <remarks>
/// <para>
/// Mỗi lớp có <b>đúng một giáo viên</b>, ghi ngay trên dòng (cột Giáo viên phụ trách, bằng email). Lớp mới luôn là bản nháp
/// giống lập lớp từng cái: còn phải xếp học sinh vào trước khi công bố. Mỗi lớp được ghi một dòng lịch sử "lập lớp" như lập tay.
/// </para>
/// <para>
/// Tên lớp là khoá nhận diện (không phân biệt hoa thường và dấu, như khi xếp học sinh vào lớp). Lớp đã có cùng giáo viên thì
/// dòng đó tính là đã nhập; lớp đã có mà giáo viên khác thì báo lỗi, vì đổi giáo viên giữa chừng phải ghi lý do và làm ở
/// trang lớp — file Excel không có chỗ cho lý do.
/// </para>
/// </remarks>
public sealed class GroupImportService : IGroupImportService
{
    /// <summary>Số dòng tối đa mỗi lần nhập.</summary>
    public const int MaxRows = 500;

    private const string AdminOnlyMessage = "Chỉ quản trị viên mới nhập lớp từ Excel.";

    private const int MaxTermNameLength = SaveAcademicTermRequestValidator.MaxNameLength;

    private static readonly IReadOnlyList<SpreadsheetColumn> Columns =
    [
        new("name", "Tên lớp", true, ["Lớp", "Tên nhóm lớp", "Nhóm lớp", "Class"]),
        new("teacher", "Giáo viên phụ trách", true, ["Email giáo viên", "Giáo viên", "Email giáo viên phụ trách", "GVCN", "Teacher"]),
        new("description", "Mô tả", false, ["Ghi chú", "Description"]),
        new("term", "Kỳ học", false, ["Học kỳ", "Kỳ", "Năm học", "Term"])
    ];

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ISpreadsheetService _spreadsheets;

    public GroupImportService(IUnitOfWork unitOfWork, ICurrentUser currentUser, ISpreadsheetService spreadsheets)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _spreadsheets = spreadsheets;
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> BuildTemplateAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<byte[]>(AdminOnlyMessage);
        }

        var teachers = await _unitOfWork.Users.GetTeachersAsync(cancellationToken);
        var emails = teachers
            .Where(teacher => teacher.Status == UserStatus.Active)
            .Select(teacher => teacher.Email)
            .ToList();

        var template = new SpreadsheetTemplate(
            "Lớp",
            [
                new TemplateColumn("Tên lớp*", 26),
                new TemplateColumn("Giáo viên phụ trách*", 34, emails.Count > 0 ? emails : null),
                new TemplateColumn("Mô tả", 40)
            ],
            [
                "Hướng dẫn nhập lớp từ Excel",
                "Điền dữ liệu ở trang \"Lớp\", bắt đầu từ dòng 2. Giữ nguyên hàng tiêu đề (dòng 1).",
                $"Mỗi lần nhập tối đa {MaxRows} dòng.",
                "Cột có dấu * là bắt buộc: Tên lớp và Giáo viên phụ trách. Mỗi lớp có đúng một giáo viên.",
                "Giáo viên phụ trách: ghi email của giáo viên (chọn trong danh sách ở trang \"Danh sách\", chỉ gồm giáo viên đang hoạt động). Giáo viên chưa có tài khoản thì nhập giáo viên trước.",
                "Tên lớp phải khác nhau. Lớp đã có cùng giáo viên thì dòng đó được tính là đã nhập rồi; lớp đã có mà giáo viên khác thì bị báo lỗi — đổi giáo viên làm ở trang lớp và phải ghi lý do.",
                "Lớp mới ở trạng thái nháp: xếp học sinh vào rồi mới công bố cho giáo viên và học sinh thấy.",
                "Dòng có lỗi sẽ được liệt kê (kèm số dòng và lý do), các dòng còn lại vẫn được nhập. Sửa xong có thể gửi lại nguyên file."
            ],
            MaxRows);

        return Result.Success(_spreadsheets.BuildTemplate(template));
    }

    /// <inheritdoc />
    public Task<Result<ImportResultDto>> ImportAsync(
        Stream file,
        ImportGroupsOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        return ImportCoreAsync(() => _spreadsheets.Read(file, Columns, MaxRows), options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<ImportResultDto>> ImportRowsAsync(
        IReadOnlyList<SpreadsheetRow> rows,
        ImportGroupsOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return ImportCoreAsync(() => Result.Success(new SpreadsheetData(rows)), options, cancellationToken);
    }

    /// <summary>Một đường mã cho cả hai cách vào: đọc từ file, hoặc nhận sẵn các dòng (nhập tổng hợp dựng dòng từ một file duy nhất).</summary>
    private async Task<Result<ImportResultDto>> ImportCoreAsync(
        Func<Result<SpreadsheetData>> readData,
        ImportGroupsOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<ImportResultDto>(AdminOnlyMessage);
        }

        var data = readData();
        if (data.IsFailure)
        {
            return Result.Failure<ImportResultDto>(data.Error!, data.ErrorCode);
        }

        var rows = data.ValueOrThrow.Rows;
        if (rows.Count == 0)
        {
            return Result.Failure<ImportResultDto>("File không có dòng dữ liệu nào để nhập.");
        }

        var teachers = await _unitOfWork.Users.GetTeachersAsync(cancellationToken);
        var teacherByEmail = new Dictionary<string, User>();
        foreach (var teacher in teachers)
        {
            teacherByEmail.TryAdd(teacher.Email.ToLowerInvariant(), teacher);
        }

        // Kỳ học đã có (so không phân biệt hoa thường và dấu); kỳ file ghi mà chưa có thì tạo cùng lớp.
        var termByName = new Dictionary<string, AcademicTerm>();
        foreach (var term in await _unitOfWork.AcademicTerms.GetAllOrderedAsync(cancellationToken))
        {
            termByName.TryAdd(ImportText.Fold(term.Name), term);
        }

        var existingGroups = await _unitOfWork.StudentGroups.GetAllAsync(search: null, includeDrafts: true, cancellationToken);
        var groupByName = new Dictionary<string, StudentGroup>();
        foreach (var group in existingGroups)
        {
            groupByName.TryAdd(ImportText.Fold(group.Name), group);
        }

        var issues = new ImportIssueCollector();

        // ── 1. Đọc và kiểm từng dòng ────────────────────────────────────────────────────────────
        var parsed = new List<ParsedRow>();
        foreach (var row in rows)
        {
            if (TryParse(row, teacherByEmail, out var item, out var column, out var error))
            {
                parsed.Add(item!);
            }
            else
            {
                issues.Reject(row.Number, column, error!);
            }
        }

        // ── 2. Trùng tên lớp ngay trong file, rồi đối chiếu với lớp đã có ────────────────────────
        var seenName = new Dictionary<string, int>();
        var toCreate = new List<ParsedRow>();
        var unchanged = 0;

        foreach (var item in parsed)
        {
            var key = ImportText.Fold(item.Name);

            if (seenName.TryGetValue(key, out var firstRow))
            {
                issues.Reject(item.Row, "Tên lớp", $"Tên lớp trùng với dòng {firstRow} trong file.");
                continue;
            }

            seenName[key] = item.Row;

            if (!groupByName.TryGetValue(key, out var existing))
            {
                toCreate.Add(item);
                continue;
            }

            var currentTeacher = existing.Teachers.FirstOrDefault();
            if (currentTeacher is null)
            {
                issues.Reject(
                    item.Row,
                    "Tên lớp",
                    $"Lớp “{existing.Name}” đã có nhưng chưa có giáo viên phụ trách. Chọn giáo viên ở trang lớp.");
            }
            else if (currentTeacher.TeacherId == item.Teacher.Id && existing.Teachers.Count == 1)
            {
                unchanged++;
            }
            else
            {
                var currentName = currentTeacher.Teacher?.FullName ?? "giáo viên khác";

                issues.Reject(
                    item.Row,
                    "Tên lớp",
                    $"Lớp “{existing.Name}” đã có, do {currentName} phụ trách. Đổi giáo viên làm ở trang lớp và phải ghi lý do.");
            }
        }

        var failed = issues.FailedRows;
        var (shownIssues, truncated) = issues.Snapshot();

        // Kỳ mới = kỳ ghi trên dòng của một lớp SẼ được tạo mà hệ thống chưa có (đã bỏ trùng, giữ cách viết của lần ghi đầu tiên).
        var newTermNames = toCreate
            .Where(item => item.Term.Length > 0 && !termByName.ContainsKey(ImportText.Fold(item.Term)))
            .GroupBy(item => ImportText.Fold(item.Term))
            .Select(group => group.First().Term)
            .ToList();

        if (options.DryRun || toCreate.Count == 0)
        {
            return Result.Success(new ImportResultDto
            {
                DryRun = options.DryRun,
                TotalRows = rows.Count,
                Created = toCreate.Count,
                Placed = 0,
                Unchanged = unchanged,
                Failed = failed,
                Issues = shownIssues,
                IssuesTruncated = truncated,
                Accounts = [],
                TermsCreated = newTermNames
            });
        }

        // ── 3. Ghi một lần: hoặc cả mẻ vào, hoặc không lớp nào vào ──────────────────────────────
        var actorId = _currentUser.RequireUserId();
        var now = DateTime.UtcNow;
        var groups = new List<StudentGroup>(toCreate.Count);
        var changes = new List<GroupChange>(toCreate.Count);

        foreach (var item in toCreate)
        {
            AcademicTerm? term = null;
            if (item.Term.Length > 0)
            {
                var termKey = ImportText.Fold(item.Term);
                if (!termByName.TryGetValue(termKey, out term))
                {
                    term = new AcademicTerm { Name = item.Term };
                    termByName[termKey] = term;
                }
            }

            var group = new StudentGroup
            {
                Name = item.Name,
                Description = item.Description,
                CreatedByUserId = actorId,

                // Kỳ có sẵn đọc không theo dõi (AsNoTracking): chỉ gán khoá ngoại, gán cả đối tượng là EF chèn lại nó như kỳ mới.
                AcademicTermId = term is { Id: > 0 } ? term.Id : null,
                AcademicTerm = term is { Id: 0 } ? term : null,

                // Giống lập lớp từng cái: lớp mới luôn là bản nháp, còn phải xếp học sinh trước khi công bố.
                IsPublished = false
            };

            group.Teachers.Add(new StudentGroupTeacher { TeacherId = item.Teacher.Id, AssignedAt = now });

            groups.Add(group);
            changes.Add(new GroupChange
            {
                Group = group,
                Kind = GroupChangeKind.Created,
                ToTeacherId = item.Teacher.Id,
                ToTeacherName = item.Teacher.FullName,
                ActorUserId = actorId
            });
        }

        await _unitOfWork.StudentGroups.AddRangeAsync(groups, cancellationToken);
        await _unitOfWork.GroupChanges.AddRangeAsync(changes, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ImportResultDto
        {
            DryRun = false,
            TotalRows = rows.Count,
            Created = groups.Count,
            Placed = 0,
            Unchanged = unchanged,
            Failed = failed,
            Issues = shownIssues,
            IssuesTruncated = truncated,
            Accounts = [],
            TermsCreated = newTermNames
        });
    }

    private sealed record ParsedRow(int Row, string Name, string? Description, User Teacher, string Term);

    private static bool TryParse(
        SpreadsheetRow row,
        IReadOnlyDictionary<string, User> teacherByEmail,
        out ParsedRow? parsed,
        out string? column,
        out string? error)
    {
        parsed = null;
        column = null;
        error = null;

        string Cell(string key) => row.Cells.TryGetValue(key, out var value) ? value : string.Empty;

        var name = ImportText.Collapse(Cell("name"));
        if (name.Length == 0)
        {
            column = "Tên lớp";
            error = "Chưa có tên lớp.";
            return false;
        }

        if (name.Length < SaveStudentGroupRequestValidator.MinNameLength)
        {
            column = "Tên lớp";
            error = $"Tên lớp phải có ít nhất {SaveStudentGroupRequestValidator.MinNameLength} ký tự.";
            return false;
        }

        if (name.Length > SaveStudentGroupRequestValidator.MaxNameLength)
        {
            column = "Tên lớp";
            error = $"Tên lớp dài quá {SaveStudentGroupRequestValidator.MaxNameLength} ký tự.";
            return false;
        }

        var description = ImportText.Collapse(Cell("description"));
        if (description.Length > SaveStudentGroupRequestValidator.MaxDescriptionLength)
        {
            column = "Mô tả";
            error = $"Mô tả dài quá {SaveStudentGroupRequestValidator.MaxDescriptionLength} ký tự.";
            return false;
        }

        var term = ImportText.Collapse(Cell("term"));
        if (term.Length > MaxTermNameLength)
        {
            column = "Kỳ học";
            error = $"Tên kỳ học dài quá {MaxTermNameLength} ký tự.";
            return false;
        }

        var email = ImportText.Collapse(Cell("teacher"));
        if (email.Length == 0)
        {
            column = "Giáo viên phụ trách";
            error = "Chưa có giáo viên phụ trách. Mỗi lớp phải có một giáo viên.";
            return false;
        }

        if (!teacherByEmail.TryGetValue(email.ToLowerInvariant(), out var teacher))
        {
            column = "Giáo viên phụ trách";
            error = $"Không có giáo viên nào dùng email “{email}”. Nhập giáo viên trước, hoặc kiểm tra lại email.";
            return false;
        }

        // Giao lớp cho một người không đăng nhập nổi là tạo ra một lớp trông như đã có người phụ trách trong khi thực tế
        // thì không — cùng luật với lập lớp từng cái.
        if (teacher.Status != UserStatus.Active)
        {
            column = "Giáo viên phụ trách";
            error = $"Tài khoản của {teacher.FullName} đang bị khoá nên chưa phân công được. Mở khoá hoặc chọn giáo viên khác.";
            return false;
        }

        parsed = new ParsedRow(row.Number, name, description.Length > 0 ? description : null, teacher, term);
        return true;
    }
}
