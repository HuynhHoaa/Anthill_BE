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
/// Nhập giáo viên từ file Excel: đầu năm học nhà trường có cả chục đến vài trăm giáo viên mới, và mỗi người là một tài khoản
/// kèm môn giảng dạy.
/// </summary>
/// <remarks>
/// <para>
/// Cùng nguyên tắc với <see cref="StudentImportService"/>: dòng hỏng không làm hỏng cả mẻ, kiểm tra thử và nhập thật chạy chung
/// một đường mã, mật khẩu ban đầu chung cho cả đợt và ép đổi ở lần đăng nhập đầu, hồ sơ có sẵn không bao giờ bị ghi đè từ file.
/// </para>
/// <para>
/// <b>Email là khoá nhận diện</b> (cũng là tên đăng nhập, như khi tạo từng giáo viên). Email đã thuộc đúng giáo viên
/// cùng họ tên thì dòng đó được tính là đã nhập (gửi lại nguyên file sau khi sửa lỗi là an toàn); đã thuộc người khác hay khác
/// họ tên thì báo lỗi để người nhập kiểm tra, vì đó gần như chắc chắn là gõ nhầm.
/// </para>
/// </remarks>
public sealed class TeacherImportService : ITeacherImportService
{
    /// <summary>Số dòng tối đa mỗi lần nhập. Giáo viên của một trường ít hơn học sinh rất nhiều.</summary>
    public const int MaxRows = 1000;

    private const int MaxFullNameLength = 128;
    private const int MaxEmailLength = 256;

    private const string AdminOnlyMessage = "Chỉ quản trị viên mới nhập giáo viên từ Excel.";

    private static readonly IReadOnlyList<SpreadsheetColumn> Columns =
    [
        new("fullName", "Họ và tên", true, ["Họ tên", "Tên giáo viên", "Full name"]),
        new("email", "Email", true, ["Thư điện tử", "E-mail"]),
        new("phone", "Số điện thoại", true, ["SĐT", "Điện thoại", "Phone"]),
        new("subject", "Môn giảng dạy", false, ["Môn", "Môn học", "Bộ môn", "Tổ bộ môn", "Subject"])
    ];

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUser _currentUser;
    private readonly ISpreadsheetService _spreadsheets;

    public TeacherImportService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ICurrentUser currentUser,
        ISpreadsheetService spreadsheets)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
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

        var subjects = await _unitOfWork.Subjects.GetAllAsync(cancellationToken);
        var names = subjects.Select(subject => subject.Name).OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList();

        var template = new SpreadsheetTemplate(
            "Giáo viên",
            [
                new TemplateColumn("Họ và tên*", 30),
                new TemplateColumn("Email*", 32),
                new TemplateColumn("Số điện thoại*", 16),
                new TemplateColumn("Môn giảng dạy", 24, names.Count > 0 ? names : null)
            ],
            [
                "Hướng dẫn nhập giáo viên từ Excel",
                "Điền dữ liệu ở trang \"Giáo viên\", bắt đầu từ dòng 2. Giữ nguyên hàng tiêu đề (dòng 1).",
                $"Mỗi lần nhập tối đa {MaxRows} dòng.",
                "Cột có dấu * là bắt buộc: Họ và tên, Email và Số điện thoại. Email chính là tên đăng nhập của giáo viên và không được trùng.",
                "Môn giảng dạy: chọn trong danh sách (trang \"Danh sách\"), hoặc gõ tên môn hay mã môn. Để trống nếu chọn môn chung ở hộp nhập. Mỗi giáo viên phải có một môn.",
                "Email đã có trong hệ thống: nếu là giáo viên cùng họ tên thì dòng đó được tính là đã nhập rồi (không tạo thêm, không sửa hồ sơ); nếu khác họ tên hoặc là tài khoản khác thì dòng đó bị báo lỗi.",
                "Số điện thoại: bắt buộc, 9 đến 15 chữ số.",
                "Mật khẩu ban đầu: nhập một lần ở hộp nhập trên web, dùng chung cho cả đợt. Giáo viên bắt buộc đổi mật khẩu ở lần đăng nhập đầu tiên.",
                "Dòng có lỗi sẽ được liệt kê (kèm số dòng và lý do), các dòng còn lại vẫn được nhập. Sửa xong có thể gửi lại nguyên file."
            ],
            MaxRows);

        return Result.Success(_spreadsheets.BuildTemplate(template));
    }

    /// <inheritdoc />
    public Task<Result<ImportResultDto>> ImportAsync(
        Stream file,
        ImportTeachersOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        return ImportCoreAsync(() => _spreadsheets.Read(file, Columns, MaxRows), options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<ImportResultDto>> ImportRowsAsync(
        IReadOnlyList<SpreadsheetRow> rows,
        ImportTeachersOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return ImportCoreAsync(() => Result.Success(new SpreadsheetData(rows)), options, cancellationToken);
    }

    /// <summary>Một đường mã cho cả hai cách vào: đọc từ file, hoặc nhận sẵn các dòng (nhập tổng hợp dựng dòng từ một file duy nhất).</summary>
    private async Task<Result<ImportResultDto>> ImportCoreAsync(
        Func<Result<SpreadsheetData>> readData,
        ImportTeachersOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<ImportResultDto>(AdminOnlyMessage);
        }

        var password = options.Password;
        if (!string.IsNullOrWhiteSpace(password) && password.Length < ValidationPatterns.MinPasswordLength)
        {
            return Result.Failure<ImportResultDto>(
                $"Mật khẩu ban đầu phải có ít nhất {ValidationPatterns.MinPasswordLength} ký tự.");
        }

        var subjects = await _unitOfWork.Subjects.GetAllAsync(cancellationToken);
        var subjectByKey = new Dictionary<string, Subject>();
        foreach (var subject in subjects)
        {
            subjectByKey.TryAdd(ImportText.Fold(subject.Name), subject);
            subjectByKey.TryAdd(ImportText.Fold(subject.Code), subject);
        }

        Subject? defaultSubject = null;
        if (options.DefaultSubjectId is { } defaultSubjectId)
        {
            defaultSubject = subjects.FirstOrDefault(subject => subject.Id == defaultSubjectId);
            if (defaultSubject is null)
            {
                return Result.Failure<ImportResultDto>("Môn đã chọn không tồn tại.");
            }
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

        var issues = new ImportIssueCollector();

        // ── 1. Đọc và kiểm từng dòng ────────────────────────────────────────────────────────────
        var parsed = new List<ParsedRow>();
        foreach (var row in rows)
        {
            if (TryParse(row, subjectByKey, defaultSubject, out var item, out var column, out var error))
            {
                parsed.Add(item!);
            }
            else
            {
                issues.Reject(row.Number, column, error!);
            }
        }

        // ── 2. Trùng email ngay trong file: dòng đầu giữ email, các dòng sau bị từ chối ─────────
        var seenEmail = new Dictionary<string, int>();
        var unique = new List<ParsedRow>();
        foreach (var item in parsed)
        {
            if (seenEmail.TryGetValue(item.Email.ToLowerInvariant(), out var firstRow))
            {
                issues.Reject(item.Row, "Email", $"Email trùng với dòng {firstRow} trong file.");
                continue;
            }

            seenEmail[item.Email.ToLowerInvariant()] = item.Row;
            unique.Add(item);
        }

        // ── 3. Đối chiếu với tài khoản đã có ────────────────────────────────────────────────────
        var existing = await _unitOfWork.Users.FindByIdentifiersAsync(
            unique.Select(item => item.Email.ToLowerInvariant()).Distinct().ToList(),
            cancellationToken);

        var holderByIdentifier = new Dictionary<string, User>();
        foreach (var user in existing)
        {
            foreach (var identifier in new[] { user.Email, user.Username, user.Code })
            {
                if (!string.IsNullOrEmpty(identifier))
                {
                    holderByIdentifier.TryAdd(identifier.ToLowerInvariant(), user);
                }
            }
        }

        var toCreate = new List<ParsedRow>();
        var unchanged = 0;

        foreach (var item in unique)
        {
            if (!holderByIdentifier.TryGetValue(item.Email.ToLowerInvariant(), out var holder))
            {
                toCreate.Add(item);
                continue;
            }

            if (holder.Role == UserRole.Teacher
                && string.Equals(holder.Email, item.Email, StringComparison.OrdinalIgnoreCase)
                && ImportText.Fold(holder.FullName) == ImportText.Fold(item.FullName))
            {
                unchanged++;
                continue;
            }

            issues.Reject(
                item.Row,
                "Email",
                holder.Role == UserRole.Teacher
                    ? $"Email “{item.Email}” đã thuộc giáo viên {holder.FullName}. Kiểm tra lại email hoặc họ tên."
                    : $"Email “{item.Email}” đã có người dùng.");
        }

        var failed = issues.FailedRows;
        var (shownIssues, truncated) = issues.Snapshot();

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
                Accounts = []
            });
        }

        // ── 4. Ghi một lần: hoặc cả mẻ vào, hoặc không ai vào ───────────────────────────────────
        if (string.IsNullOrWhiteSpace(password))
        {
            return Result.Failure<ImportResultDto>("Hãy nhập mật khẩu ban đầu cho các tài khoản mới.");
        }

        var passwordHash = _passwordHasher.Hash(password);

        var teachers = toCreate
            .Select(item => new User
            {
                FullName = item.FullName,
                Email = item.Email,

                // Email là tên đăng nhập của giáo viên, giống tạo từng người một.
                Username = item.Email,
                Phone = item.Phone,
                PasswordHash = passwordHash,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                Role = UserRole.Teacher,
                Status = UserStatus.Active,
                SubjectId = item.Subject.Id,
                AvatarColor = AvatarPalette.ForKey(item.Email),

                // §5: mật khẩu do quản trị viên cấp thì ép đổi ở lần đăng nhập đầu.
                MustChangePassword = true
            })
            .ToList();

        await _unitOfWork.Users.AddRangeAsync(teachers, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ImportResultDto
        {
            DryRun = false,
            TotalRows = rows.Count,
            Created = teachers.Count,
            Placed = 0,
            Unchanged = unchanged,
            Failed = failed,
            Issues = shownIssues,
            IssuesTruncated = truncated,
            Accounts = toCreate
                .Select(item => new ImportedAccountDto
                {
                    FullName = item.FullName,
                    Username = item.Email,
                    Group = item.Subject.Name
                })
                .ToList()
        });
    }

    private sealed record ParsedRow(int Row, string FullName, string Email, string? Phone, Subject Subject);

    private static bool TryParse(
        SpreadsheetRow row,
        IReadOnlyDictionary<string, Subject> subjectByKey,
        Subject? defaultSubject,
        out ParsedRow? parsed,
        out string? column,
        out string? error)
    {
        parsed = null;
        column = null;
        error = null;

        string Cell(string key) => row.Cells.TryGetValue(key, out var value) ? value : string.Empty;

        var fullName = ImportText.Collapse(Cell("fullName"));
        if (fullName.Length == 0)
        {
            column = "Họ và tên";
            error = "Chưa có họ và tên.";
            return false;
        }

        if (fullName.Length > MaxFullNameLength)
        {
            column = "Họ và tên";
            error = $"Họ và tên dài quá {MaxFullNameLength} ký tự.";
            return false;
        }

        var email = ImportText.Collapse(Cell("email"));
        if (email.Length == 0)
        {
            column = "Email";
            error = "Chưa có email.";
            return false;
        }

        if (email.Length > MaxEmailLength || !ValidationPatterns.Email.IsMatch(email))
        {
            column = "Email";
            error = "Email không đúng định dạng.";
            return false;
        }

        var phone = ImportText.Phone(Cell("phone"));
        if (phone.Length == 0)
        {
            column = "Số điện thoại";
            error = "Chưa có số điện thoại.";
            return false;
        }

        if (!ValidationPatterns.Phone.IsMatch(phone))
        {
            column = "Số điện thoại";
            error = "Số điện thoại không hợp lệ (9 đến 15 chữ số).";
            return false;
        }

        var subjectText = ImportText.Collapse(Cell("subject"));
        var subject = defaultSubject;
        if (subjectText.Length > 0)
        {
            if (!subjectByKey.TryGetValue(ImportText.Fold(subjectText), out subject))
            {
                column = "Môn giảng dạy";
                error = $"Môn “{subjectText}” chưa có trong hệ thống.";
                return false;
            }
        }

        if (subject is null)
        {
            column = "Môn giảng dạy";
            error = "Chưa có môn giảng dạy. Điền vào ô Môn hoặc chọn môn chung ở hộp nhập.";
            return false;
        }

        parsed = new ParsedRow(row.Number, fullName, email, phone, subject);
        return true;
    }
}
