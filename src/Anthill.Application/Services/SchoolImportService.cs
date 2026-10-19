using Anthill.Application.Common;
using Anthill.Application.DTOs.Imports;
using Anthill.Application.Interfaces.Files;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Validators;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using System.Text.RegularExpressions;

namespace Anthill.Application.Services;

/// <summary>
/// Nhập tổng hợp: một trang Excel, mỗi dòng một học sinh kèm lớp và giáo viên chủ nhiệm — xem <see cref="ISchoolImportService"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Không viết lại luật nhập.</b> <see cref="SchoolImportPlanner"/> chia file thành ba danh sách dòng, rồi ba bộ nhập đã có
/// (giáo viên → lớp → học sinh) xử lý từng danh sách theo đúng luật của chúng: email dùng chung một không gian tên, giáo viên bị khoá
/// không nhận lớp, hồ sơ có sẵn không bị ghi đè, mã học sinh là khoá... Nhập tổng hợp không có cửa hậu nào mà nhập riêng không có.
/// </para>
/// <para>
/// <b>Một transaction cho cả ba bước.</b> Bước sau cần thấy kết quả bước trước (lớp tham chiếu giáo viên mới tạo, học sinh tham chiếu
/// lớp mới tạo), mà "nhập thử" không ghi gì thì không thấy được. Nên nhập thử cũng chạy thật rồi <b>rollback</b>: con số xem trước là
/// con số sẽ xảy ra. Lỗi hạ tầng (mật khẩu thiếu, môn không có...) làm cả transaction rollback; lỗi từng dòng thì không.
/// </para>
/// <para>
/// <b>Lỗi lan theo quan hệ.</b> Giáo viên hỏng thì lớp của người đó không tạo được; lớp hỏng thì các học sinh của lớp chưa được xếp.
/// Nguyên nhân gốc báo một lần ở dòng đầu tiên, các học sinh còn lại báo "chưa được xếp" kèm số dòng gốc — không trút hàng trăm dòng
/// cùng một lỗi.
/// </para>
/// </remarks>
public sealed class SchoolImportService : ISchoolImportService
{
    /// <summary>Số dòng tối đa mỗi lần nhập (bằng giới hạn của nhập học sinh).</summary>
    public const int MaxRows = 20000;

    /// <summary>Cột Tên của bảng Subjects dài 128; tên dài hơn không được tạo thành môn (dòng đó báo "môn chưa có trong hệ thống").</summary>
    private const int MaxSubjectNameLength = 128;

    private const string AdminOnlyMessage = "Chỉ quản trị viên mới nhập danh sách từ Excel.";

    /// <summary>Nhập thử cần một mật khẩu để chạy hết đường mã (người dùng chưa phải nhập); không bao giờ được ghi thật.</summary>
    private const string PreviewPassword = "Xem-truoc-1";

    private static readonly IReadOnlyList<SpreadsheetColumn> Columns =
    [
        new("code", "Mã học sinh", true, ["Mã HS", "MSHS", "Mã số học sinh", "Code"]),
        new("fullName", "Họ và tên", true, ["Họ tên", "Tên học sinh", "Full name"]),
        new("group", "Lớp", true, ["Tên lớp", "Nhóm lớp", "Nhóm/lớp", "Class"]),
        new("teacherName", "Giáo viên chủ nhiệm", true, ["GVCN", "Chủ nhiệm", "Họ tên giáo viên", "Tên giáo viên", "Tên GVCN"]),
        new("teacherEmail", "Email giáo viên", true, ["Email GVCN", "Email chủ nhiệm", "Email giáo viên chủ nhiệm"]),
        new("teacherPhone", "Số điện thoại giáo viên", true, ["SĐT giáo viên", "SĐT GVCN", "Điện thoại giáo viên", "Số điện thoại GVCN"]),
        new("subject", "Môn giảng dạy", false, ["Môn", "Môn GVCN", "Bộ môn", "Môn giáo viên"]),
        new("email", "Email học sinh", false, ["Email", "Thư điện tử", "E-mail"]),
        new("phone", "Số điện thoại học sinh", false, ["SĐT học sinh", "Số điện thoại", "SĐT", "Điện thoại"]),
        new("username", "Tên đăng nhập", false, ["Username", "Tài khoản"]),
        new("term", "Kỳ học", false, ["Học kỳ", "Kỳ", "Năm học", "Term"])
    ];

    // Tên cột trong ba bộ nhập riêng → tên cột trong file tổng hợp, để lỗi chỉ đúng ô người dùng đang nhìn.
    private static readonly IReadOnlyDictionary<string, string> TeacherColumnNames = new Dictionary<string, string>
    {
        ["Họ và tên"] = "Giáo viên chủ nhiệm",
        ["Email"] = "Email giáo viên",
        ["Số điện thoại"] = "Số điện thoại giáo viên"
    };

    private static readonly IReadOnlyDictionary<string, string> GroupColumnNames = new Dictionary<string, string>
    {
        ["Tên lớp"] = "Lớp",
        ["Giáo viên phụ trách"] = "Email giáo viên",
        ["Mô tả"] = "Lớp",
        ["Kỳ học"] = "Kỳ học"
    };

    private static readonly IReadOnlyDictionary<string, string> StudentColumnNames = new Dictionary<string, string>
    {
        ["Email"] = "Email học sinh",
        ["Số điện thoại"] = "Số điện thoại học sinh"
    };

    /// <summary>
    /// Kết quả của một bước không có dòng nào để làm (mọi giáo viên đã hỏng nên không lớp nào nhập được, hoặc ngược lại). Bộ nhập con coi
    /// danh sách rỗng là lỗi "file trống", điều đúng với một file nhưng sai với một bước ở giữa: các lỗi từng dòng của bước trước mới là
    /// thứ người dùng cần đọc.
    /// </summary>
    private static readonly ImportResultDto NothingToDo = new()
    {
        DryRun = false,
        TotalRows = 0,
        Created = 0,
        Placed = 0,
        Unchanged = 0,
        Failed = 0,
        Issues = [],
        IssuesTruncated = false,
        Accounts = []
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ISpreadsheetService _spreadsheets;
    private readonly ITeacherImportService _teachers;
    private readonly IGroupImportService _groups;
    private readonly IStudentImportService _students;

    public SchoolImportService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        ISpreadsheetService spreadsheets,
        ITeacherImportService teachers,
        IGroupImportService groups,
        IStudentImportService students)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _spreadsheets = spreadsheets;
        _teachers = teachers;
        _groups = groups;
        _students = students;
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> BuildTemplateAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<byte[]>(AdminOnlyMessage);
        }

        var subjects = await _unitOfWork.Subjects.GetAllAsync(cancellationToken);
        var names = subjects.Select(subject => subject.Name).OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var termNames = (await _unitOfWork.AcademicTerms.GetAllOrderedAsync(cancellationToken)).Select(term => term.Name).ToList();

        var template = new SpreadsheetTemplate(
            "Học sinh",
            [
                new TemplateColumn("Mã học sinh*", 16),
                new TemplateColumn("Họ và tên*", 28),
                new TemplateColumn("Lớp*", 14),
                new TemplateColumn("Giáo viên chủ nhiệm*", 26),
                new TemplateColumn("Email giáo viên*", 30),
                new TemplateColumn("Số điện thoại giáo viên*", 20),
                new TemplateColumn("Môn giảng dạy", 24, names.Count > 0 ? names : null),
                new TemplateColumn("Email học sinh", 30),
                new TemplateColumn("Số điện thoại học sinh", 20),
                new TemplateColumn("Tên đăng nhập", 18),
                new TemplateColumn("Kỳ học", 24, termNames.Count > 0 ? termNames : null)
            ],
            [
                "Hướng dẫn nhập danh sách trường từ Excel (một file cho cả giáo viên, lớp và học sinh)",
                "Mỗi dòng là một học sinh. Điền ở trang \"Học sinh\", bắt đầu từ dòng 2. Giữ nguyên hàng tiêu đề (dòng 1).",
                $"Mỗi lần nhập tối đa {MaxRows} dòng.",
                "Cột có dấu * là bắt buộc: Mã học sinh, Họ và tên, Lớp, Giáo viên chủ nhiệm, Email giáo viên và Số điện thoại giáo viên.",
                "Lớp và giáo viên được tạo từ chính các dòng này: cùng tên lớp là một lớp, cùng email giáo viên là một người. Hệ thống tạo giáo viên, tạo lớp (kèm giáo viên chủ nhiệm) và xếp học sinh vào lớp trong một lần.",
                "Thông tin giáo viên chủ nhiệm chỉ cần điền một lần cho mỗi lớp (ở dòng đầu của lớp); các dòng sau của lớp để trống hoặc điền lặp lại đều được. Hai dòng cùng lớp mà ghi hai giáo viên khác nhau thì dòng sau bị báo lỗi.",
                "Môn giảng dạy của giáo viên: chọn trong danh sách (trang \"Danh sách\") hoặc gõ tên môn; môn chưa có trong hệ thống sẽ được tạo mới (bước xem trước liệt kê để bạn soát lại). Giáo viên đã có trong hệ thống có thể để trống ô này.",
                "Kỳ học (cột cuối) không bắt buộc: ghi kỳ của lớp (ví dụ \"Học kỳ 1 · 2026–2027\" hay \"Lớp 11\"), một lần cho mỗi lớp; kỳ chưa có trong hệ thống sẽ được tạo mới và bước xem trước liệt kê. Học sinh lọc lớp và bảng điểm theo kỳ.",
                "Email học sinh, số điện thoại học sinh và tên đăng nhập không bắt buộc. Tên đăng nhập để trống thì dùng mã học sinh; email để trống thì hệ thống cấp email tạm.",
                "Giáo viên hay học sinh đã có trong hệ thống: hồ sơ không bị sửa; học sinh có sẵn chỉ được xếp thêm vào lớp ghi trên dòng.",
                "Lớp mới ở trạng thái nháp: giáo viên chưa thấy cho tới khi công bố (hoặc chọn công bố ngay ở hộp nhập).",
                "Mật khẩu ban đầu: nhập một lần ở hộp nhập trên web, dùng chung cho mọi tài khoản mới (giáo viên và học sinh). Ai cũng bắt buộc đổi mật khẩu ở lần đăng nhập đầu tiên.",
                "Dòng có lỗi sẽ được liệt kê (kèm số dòng và lý do), các dòng còn lại vẫn được nhập. Lỗi ở giáo viên hay lớp thì các học sinh của lớp đó chưa được xếp; sửa xong có thể gửi lại nguyên file."
            ],
            MaxRows);

        return Result.Success(_spreadsheets.BuildTemplate(template));
    }

    /// <inheritdoc />
    public async Task<Result<SchoolImportResultDto>> ImportAsync(
        Stream file,
        ImportSchoolOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(options);

        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<SchoolImportResultDto>(AdminOnlyMessage);
        }

        if (!string.IsNullOrWhiteSpace(options.Password) && options.Password.Length < ValidationPatterns.MinPasswordLength)
        {
            return Result.Failure<SchoolImportResultDto>(
                $"Mật khẩu ban đầu phải có ít nhất {ValidationPatterns.MinPasswordLength} ký tự.");
        }

        var data = _spreadsheets.Read(file, Columns, MaxRows);
        if (data.IsFailure)
        {
            return Result.Failure<SchoolImportResultDto>(data.Error!, data.ErrorCode);
        }

        var rows = data.ValueOrThrow.Rows;
        if (rows.Count == 0)
        {
            return Result.Failure<SchoolImportResultDto>("File không có dòng dữ liệu nào để nhập.");
        }

        var plan = SchoolImportPlanner.Build(rows);
        var issues = new ImportIssueCollector();
        foreach (var issue in plan.Issues)
        {
            issues.Reject(issue.Row, issue.Column, issue.Message);
        }

        // Nhập thử chạy hết đường mã với một mật khẩu giả rồi rollback; nhập thật dùng mật khẩu của người dùng.
        var password = options.DryRun ? options.Password ?? PreviewPassword : options.Password;

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        // Người đã có trong hệ thống, đọc trước khi bất cứ bước nào ghi: sau bước giáo viên thì người mới cũng đã "có".
        IReadOnlyList<User> existingHolders = plan.Teachers.Count == 0
            ? []
            : await _unitOfWork.Users.FindByIdentifiersAsync(plan.Teachers.Select(teacher => teacher.Email).ToList(), cancellationToken);

        // ── 0. Môn học file ghi mà hệ thống chưa có (môn là cột trong file, không phải một lựa chọn ở hộp nhập) ──────────────
        var subjectsCreated = await CreateMissingSubjectsAsync(plan.Teachers, existingHolders, cancellationToken);

        // ── 1. Giáo viên chủ nhiệm ──────────────────────────────────────────────────────────────
        var teacherRows = await WithKnownSubjectsAsync(plan.Teachers, existingHolders, cancellationToken);
        var teacherResult = teacherRows.Count == 0
            ? Result.Success(NothingToDo)
            : await _teachers.ImportRowsAsync(
                teacherRows,
                new ImportTeachersOptions(options.DefaultSubjectId, password, DryRun: false),
                cancellationToken);
        if (teacherResult.IsFailure)
        {
            return Result.Failure<SchoolImportResultDto>(teacherResult.Error!, teacherResult.ErrorCode);
        }

        var teacherFailedRows = Absorb(issues, teacherResult.ValueOrThrow, TeacherColumnNames);

        // Môn vừa tạo mà không giáo viên nào dùng (giáo viên của dòng đó hỏng vì lý do khác) thì không để lại: gõ sai một dòng không
        // được sinh ra một môn rác.
        var usedSubjects = teacherResult.ValueOrThrow.Accounts.Select(account => ImportText.Fold(account.Group)).ToHashSet();
        var unusedSubjects = subjectsCreated.Where(subject => !usedSubjects.Contains(ImportText.Fold(subject.Name))).ToList();
        if (unusedSubjects.Count > 0)
        {
            _unitOfWork.Subjects.RemoveRange(unusedSubjects);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            subjectsCreated.RemoveAll(unusedSubjects.Contains);
        }

        // ── 2. Lớp (chỉ những lớp có giáo viên chủ nhiệm nhập được) ───────────────────────────────
        var failedTeacherEmails = plan.Teachers
            .Where(teacher => teacherFailedRows.Contains(teacher.FirstRow))
            .ToDictionary(teacher => teacher.Email, teacher => teacher.FirstRow);

        var blockedGroups = new Dictionary<string, (string Name, int RootRow)>();
        var groupsToImport = new List<PlannedGroup>();
        foreach (var group in plan.Groups)
        {
            if (failedTeacherEmails.TryGetValue(group.TeacherEmail, out var teacherRow))
            {
                blockedGroups[group.Key] = (group.Name, teacherRow);
            }
            else
            {
                groupsToImport.Add(group);
            }
        }

        var existingGroupKeys = (await _unitOfWork.StudentGroups.GetAllAsync(search: null, includeDrafts: true, cancellationToken))
            .Select(group => ImportText.Fold(group.Name))
            .ToHashSet();

        var groupResult = groupsToImport.Count == 0
            ? Result.Success(NothingToDo)
            : await _groups.ImportRowsAsync(
                groupsToImport.Select(group => group.Row).ToList(),
                new ImportGroupsOptions(DryRun: false),
                cancellationToken);
        if (groupResult.IsFailure)
        {
            return Result.Failure<SchoolImportResultDto>(groupResult.Error!, groupResult.ErrorCode);
        }

        var groupFailedRows = Absorb(issues, groupResult.ValueOrThrow, GroupColumnNames);
        foreach (var group in groupsToImport.Where(group => groupFailedRows.Contains(group.FirstRow)))
        {
            blockedGroups[group.Key] = (group.Name, group.FirstRow);
        }

        // Lớp bị chặn vì giáo viên: mọi dòng học sinh của lớp (trừ dòng gốc đã mang lỗi) báo "chưa được xếp".
        foreach (var student in plan.Students)
        {
            if (blockedGroups.TryGetValue(student.GroupKey, out var blocked) && !issues.IsRejected(student.Row))
            {
                issues.Reject(
                    student.Row,
                    "Lớp",
                    $"Lớp “{blocked.Name}” chưa nhập được (xem dòng {blocked.RootRow}), nên học sinh này chưa được xếp.");
            }
        }

        var createdGroupKeys = groupsToImport
            .Where(group => !groupFailedRows.Contains(group.FirstRow) && !existingGroupKeys.Contains(group.Key))
            .Select(group => group.Key)
            .ToHashSet();

        // Học sinh đã có tài khoản (theo mã), cũng đọc trước khi bước học sinh ghi — chỉ cần cho danh sách xem trước.
        var existingStudentCodes = new HashSet<string>();
        if (options.DryRun && plan.Students.Count > 0)
        {
            var holders = await _unitOfWork.Users.FindByIdentifiersAsync(
                plan.Students.Select(student => ImportText.Collapse(student.Source.Cells["code"]).ToLowerInvariant()).Distinct().ToList(),
                cancellationToken);
            existingStudentCodes = holders
                .Where(user => user.Role == UserRole.Student && !string.IsNullOrEmpty(user.Code))
                .Select(user => user.Code!.ToLowerInvariant())
                .ToHashSet();
        }

        // ── 3. Học sinh (chỉ những em thuộc lớp đã nhập được) ─────────────────────────────────────
        var studentRows = plan.Students
            .Where(student => !blockedGroups.ContainsKey(student.GroupKey))
            .Select(student => student.Source)
            .ToList();
        var studentResult = studentRows.Count == 0
            ? Result.Success(NothingToDo)
            : await _students.ImportRowsAsync(
                studentRows,
                new ImportStudentsOptions(DefaultGroupId: null, password, DryRun: false),
                cancellationToken);
        if (studentResult.IsFailure)
        {
            return Result.Failure<SchoolImportResultDto>(studentResult.Error!, studentResult.ErrorCode);
        }

        var studentFailedRows = Absorb(issues, studentResult.ValueOrThrow, StudentColumnNames);

        var preview = options.DryRun
            ? BuildPreview(plan, plan.Teachers.Zip(teacherRows, (teacher, row) => (teacher.Email, Row: row)).ToDictionary(pair => pair.Email, pair => pair.Row),
                existingHolders, existingGroupKeys, existingStudentCodes, blockedGroups.Keys.ToHashSet(), issues)
            : [];

        // ── 4. Công bố các lớp vừa tạo (nếu người dùng chọn) ──────────────────────────────────────
        var published = 0;
        if (options.PublishGroups && createdGroupKeys.Count > 0)
        {
            var now = DateTime.UtcNow;

            // Lấy id từ danh sách rồi nạp lại từng lớp bằng khoá: lớp vừa tạo đang được theo dõi trong cùng phiên làm việc, và đưa
            // vào một bản sao khác của cùng khoá chính sẽ làm Entity Framework từ chối.
            var all = await _unitOfWork.StudentGroups.GetAllAsync(search: null, includeDrafts: true, cancellationToken);
            var createdIds = all
                .Where(group => createdGroupKeys.Contains(ImportText.Fold(group.Name)))
                .Select(group => group.Id)
                .ToList();

            foreach (var id in createdIds)
            {
                var group = await _unitOfWork.StudentGroups.GetByIdAsync(id, cancellationToken);
                if (group is null || group.IsPublished)
                {
                    continue;
                }

                group.IsPublished = true;
                group.PublishedAt = now;
                group.UpdatedAt = now;
                _unitOfWork.StudentGroups.Update(group);
                published++;
            }

            if (published > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        if (options.DryRun)
        {
            // Mọi thứ vừa ghi chỉ để các bước sau nhìn thấy nhau; xem trước thì không giữ lại gì.
            await transaction.RollbackAsync(cancellationToken);
        }
        else
        {
            await transaction.CommitAsync(cancellationToken);
        }

        var (issueList, truncated) = issues.Snapshot();
        var teacherDto = teacherResult.ValueOrThrow;
        var groupDto = groupResult.ValueOrThrow;
        var studentDto = studentResult.ValueOrThrow;

        return Result.Success(new SchoolImportResultDto
        {
            DryRun = options.DryRun,
            TotalRows = rows.Count,
            SubjectsCreated = subjectsCreated.Select(subject => subject.Name).ToList(),
            TermsCreated = groupDto.TermsCreated,
            Preview = preview,
            TeachersCreated = teacherDto.Created,
            TeachersUnchanged = teacherDto.Unchanged,
            GroupsCreated = groupDto.Created,
            GroupsUnchanged = groupDto.Unchanged,
            GroupsPublished = published,
            StudentsCreated = studentDto.Created,
            StudentsPlaced = studentDto.Placed,
            StudentsUnchanged = studentDto.Unchanged,
            // Danh sách lỗi của bộ nhập học sinh bị cắt ở 500 dòng, nhưng số dòng hỏng của nó vẫn đếm đủ: cộng phần không có tên dòng.
            Failed = issues.FailedRows + Math.Max(0, studentDto.Failed - studentFailedRows.Count),
            Issues = issueList,
            IssuesTruncated = truncated || studentDto.IssuesTruncated,
            Accounts = options.DryRun ? [] : teacherDto.Accounts.Concat(studentDto.Accounts).ToList()
        });
    }

    /// <summary>
    /// Tạo các môn mà file ghi cho giáo viên <b>mới</b> nhưng hệ thống chưa có. Trường soạn file theo môn thật của mình, không theo danh
    /// sách môn có sẵn của phần mềm, và môn không có trang quản lý riêng: bắt nhập tay từng môn trước khi nhập file là chặn đường.
    /// Gõ sai chính tả thì ra môn sai, nên xem trước liệt kê môn mới; giáo viên có sẵn thì môn ghi trên dòng bị bỏ qua (không sửa hồ sơ).
    /// </summary>
    private async Task<List<Subject>> CreateMissingSubjectsAsync(
        IReadOnlyList<PlannedTeacher> planned,
        IReadOnlyList<User> existingHolders,
        CancellationToken cancellationToken)
    {
        if (planned.Count == 0)
        {
            return [];
        }

        var existingEmails = existingHolders.Select(user => user.Email.ToLowerInvariant()).ToHashSet();

        var wanted = new Dictionary<string, string>();
        foreach (var teacher in planned.Where(teacher => !existingEmails.Contains(teacher.Email)))
        {
            var name = teacher.Row.Cells.TryGetValue("subject", out var cell) ? ImportText.Collapse(cell) : string.Empty;
            if (name.Length is > 0 and <= MaxSubjectNameLength)
            {
                wanted.TryAdd(ImportText.Fold(name), name);
            }
        }

        if (wanted.Count == 0)
        {
            return [];
        }

        var subjects = await _unitOfWork.Subjects.GetAllAsync(cancellationToken);
        var known = subjects.SelectMany(subject => new[] { ImportText.Fold(subject.Name), ImportText.Fold(subject.Code) }).ToHashSet();
        var takenCodes = subjects.Select(subject => subject.Code.ToLowerInvariant()).ToHashSet();

        var created = new List<Subject>();
        foreach (var entry in wanted.Where(entry => !known.Contains(entry.Key)))
        {
            created.Add(new Subject { Name = entry.Value, Code = NewSubjectCode(entry.Value, takenCodes) });
        }

        if (created.Count > 0)
        {
            await _unitOfWork.Subjects.AddRangeAsync(created, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    /// <summary>
    /// Mã môn từ tên: nhiều từ thì lấy chữ cái đầu của từng từ ("Khoa học tự nhiên" → KHTN, "Anh văn" → AV), một từ thì lấy bốn chữ đầu
    /// ("Toán" → TOAN). Trùng mã thì thêm số. Mã duy nhất không phân biệt hoa thường.
    /// </summary>
    public static string NewSubjectCode(string name, ISet<string> takenCodes)
    {
        var words = Regex.Split(ImportText.Fold(name), "[^a-z0-9]+").Where(word => word.Length > 0).ToArray();
        var core = words.Length switch
        {
            0 => "MON",
            1 => words[0][..Math.Min(4, words[0].Length)],
            _ => string.Concat(words.Take(6).Select(word => word[0]))
        };

        var baseCode = core.ToUpperInvariant();
        var code = baseCode;
        for (var suffix = 2; !takenCodes.Add(code.ToLowerInvariant()); suffix++)
        {
            code = baseCode + suffix;
        }

        return code;
    }

    /// <summary>
    /// Giáo viên đã có trong hệ thống: môn lấy theo hồ sơ đang có, bất kể ô Môn trên dòng ghi gì (trống, khác, hay một môn chưa có).
    /// Trường hay giao thêm một lớp cho giáo viên cũ; hồ sơ có sẵn không bao giờ bị sửa, nên môn ghi trên dòng không có nghĩa gì với họ.
    /// Bộ nhập giáo viên vốn đòi môn hợp lệ ở mọi dòng vì nó dùng để tạo người mới, và chỉ nhận ra "đã có, không đổi" khi email và họ tên khớp.
    /// </summary>
    private async Task<IReadOnlyList<SpreadsheetRow>> WithKnownSubjectsAsync(
        IReadOnlyList<PlannedTeacher> planned,
        IReadOnlyList<User> existingHolders,
        CancellationToken cancellationToken)
    {
        if (planned.Count == 0)
        {
            return [];
        }

        var subjectIdByEmail = existingHolders
            .Where(user => user.Role == UserRole.Teacher && user.SubjectId is not null)
            .GroupBy(user => user.Email.ToLowerInvariant())
            .ToDictionary(group => group.Key, group => group.First().SubjectId!.Value);
        if (subjectIdByEmail.Count == 0)
        {
            return planned.Select(teacher => teacher.Row).ToList();
        }

        var subjectNameById = (await _unitOfWork.Subjects.GetAllAsync(cancellationToken)).ToDictionary(subject => subject.Id, subject => subject.Name);

        var rows = new List<SpreadsheetRow>(planned.Count);
        foreach (var teacher in planned)
        {
            if (subjectIdByEmail.TryGetValue(teacher.Email, out var subjectId)
                && subjectNameById.TryGetValue(subjectId, out var name))
            {
                rows.Add(new SpreadsheetRow(teacher.Row.Number, new Dictionary<string, string>(teacher.Row.Cells) { ["subject"] = name }));
            }
            else
            {
                rows.Add(teacher.Row);
            }
        }

        return rows;
    }

    /// <summary>
    /// Danh sách để quản trị viên soát trước khi nhập: từng lớp sẽ nhập được, kèm giáo viên, môn và học sinh. Lớp bị chặn và dòng hỏng
    /// không có ở đây (chúng nằm ở danh sách lỗi), nên danh sách là đúng những gì nhập thật sẽ làm.
    /// </summary>
    private static List<SchoolPreviewGroupDto> BuildPreview(
        SchoolImportPlan plan,
        IReadOnlyDictionary<string, SpreadsheetRow> teacherRowByEmail,
        IReadOnlyList<User> existingHolders,
        ISet<string> existingGroupKeys,
        ISet<string> existingStudentCodes,
        ISet<string> blockedGroupKeys,
        ImportIssueCollector issues)
    {
        var existingTeacherEmails = existingHolders
            .Where(user => user.Role == UserRole.Teacher)
            .Select(user => user.Email.ToLowerInvariant())
            .ToHashSet();

        var studentsByGroup = plan.Students
            .Where(student => !issues.IsRejected(student.Row))
            .GroupBy(student => student.GroupKey)
            .ToDictionary(group => group.Key, group => group.ToList());

        var result = new List<SchoolPreviewGroupDto>();
        foreach (var group in plan.Groups.Where(group => !blockedGroupKeys.Contains(group.Key)))
        {
            // Lớp có thể còn lại không học sinh nào (mọi em đều hỏng): lớp vẫn được tạo, nên vẫn hiện, với danh sách trống.
            var students = studentsByGroup.TryGetValue(group.Key, out var kept) ? kept : [];

            var teacher = teacherRowByEmail.TryGetValue(group.TeacherEmail, out var row) ? row : null;
            string Cell(string key) => teacher is not null && teacher.Cells.TryGetValue(key, out var value) ? ImportText.Collapse(value) : string.Empty;

            result.Add(new SchoolPreviewGroupDto(
                group.Name,
                existingGroupKeys.Contains(group.Key),
                Cell("fullName"),
                group.TeacherEmail,
                existingTeacherEmails.Contains(group.TeacherEmail),
                Cell("subject"),
                students
                    .Select(student => new SchoolPreviewStudentDto(
                        student.Row,
                        ImportText.Collapse(student.Source.Cells["code"]),
                        ImportText.Collapse(student.Source.Cells["fullName"]),
                        existingStudentCodes.Contains(ImportText.Collapse(student.Source.Cells["code"]).ToLowerInvariant())))
                    .ToList(),
                string.IsNullOrEmpty(group.Term) ? null : group.Term));
        }

        return result;
    }

    /// <summary>Đưa lỗi của một bộ nhập con vào danh sách chung (đổi tên cột cho đúng file tổng hợp); trả về các dòng đã bị từ chối.</summary>
    private static HashSet<int> Absorb(ImportIssueCollector target, ImportResultDto result, IReadOnlyDictionary<string, string> columnNames)
    {
        var rows = new HashSet<int>();
        foreach (var issue in result.Issues)
        {
            var column = issue.Column is { } name && columnNames.TryGetValue(name, out var renamed) ? renamed : issue.Column;

            // Bộ nhập giáo viên nhắc "chọn môn chung ở hộp nhập"; màn nhập cả trường không có hộp đó, môn nằm ở cột trong file.
            var message = issue.Message.StartsWith("Chưa có môn giảng dạy", StringComparison.Ordinal)
                ? "Chưa có môn giảng dạy. Điền vào ô Môn giảng dạy của giáo viên này."
                : issue.Message;
            target.Reject(issue.Row, column, message);
            rows.Add(issue.Row);
        }

        return rows;
    }
}
