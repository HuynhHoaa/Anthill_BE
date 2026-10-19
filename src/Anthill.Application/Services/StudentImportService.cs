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
/// Nhập học sinh từ file Excel: tạo tài khoản mới và xếp vào lớp, hoặc chỉ xếp vào lớp những em đã có tài khoản.
/// </summary>
/// <remarks>
/// <para>
/// Mỗi kỳ có vài nghìn học sinh, không ai nhập tay từng em. File chỉ cần <b>mã học sinh</b> và <b>họ tên</b>; email, tên đăng
/// nhập, số điện thoại và lớp là tuỳ chọn. Mật khẩu ban đầu <b>chung cho cả đợt</b> (nhập một lần ở hộp thoại, mỗi em bị ép đổi ở
/// lần đăng nhập đầu) — một mật khẩu thì băm một lần, còn băm riêng từng em thì hàng nghìn lần BCrypt mất nhiều phút.
/// </para>
/// <para>
/// <b>Mã học sinh là khoá nhận diện.</b> Mã đã có trong hệ thống thì dòng đó nói về em ấy, không tạo tài khoản mới; hồ sơ không bao
/// giờ bị ghi đè từ file (chỉ được xếp lớp). Họ tên trong file khác hẳn họ tên đã có thì báo lỗi: đó là dấu hiệu gõ nhầm mã chứ
/// không phải cùng một người. Nhờ vậy gửi lại nguyên file sau khi sửa lỗi là an toàn.
/// </para>
/// <para>
/// <b>Cùng luật với xếp lớp bằng tay.</b> Một em học được nhiều lớp cùng lúc, nên dòng ghi một lớp mà em chưa học thì em được
/// xếp <b>thêm</b> vào lớp đó (không rời lớp nào); em đã học lớp đó rồi thì dòng được tính là đã nhập. Lớp đã khoá không nhận
/// thêm em. Mỗi em được xếp ghi một dòng lịch sử như xếp lớp bằng tay.
/// </para>
/// <para>
/// Dòng hỏng không làm hỏng cả mẻ: dòng hợp lệ được nhập, dòng hỏng được liệt kê kèm số dòng và lý do. Hai bước (kiểm tra thử
/// rồi nhập thật) chạy cùng một đường mã nên bản xem trước không thể hứa điều bản nhập thật không làm.
/// </para>
/// </remarks>
public sealed class StudentImportService : IStudentImportService
{
    /// <summary>Số dòng tối đa mỗi lần nhập. Đủ cho một khối hay một khoá; nhiều hơn thì tách file.</summary>
    public const int MaxRows = 5000;

    private const int MaxFullNameLength = 128;
    private const int MaxCodeLength = 32;
    private const int MinUsernameLength = 3;
    private const int MaxUsernameLength = 64;
    private const int MaxEmailLength = 256;

    /// <summary>Miền email tạm cho học sinh không có email: ghi rõ đây không phải hộp thư thật.</summary>
    private const string PlaceholderEmailDomain = "student.anthill.local";

    private const string AdminOnlyMessage = "Chỉ quản trị viên mới nhập học sinh từ Excel.";

    private static readonly IReadOnlyList<SpreadsheetColumn> Columns =
    [
        new("code", "Mã học sinh", true, ["Mã HS", "MSHS", "Mã số học sinh", "Code"]),
        new("fullName", "Họ và tên", true, ["Họ tên", "Tên học sinh", "Full name"]),
        new("group", "Lớp", false, ["Tên lớp", "Nhóm lớp", "Nhóm/lớp", "Class"]),
        new("email", "Email", false, ["Thư điện tử", "E-mail"]),
        new("phone", "Số điện thoại", false, ["SĐT", "Điện thoại", "Phone"]),
        new("username", "Tên đăng nhập", false, ["Username", "Tài khoản"])
    ];

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUser _currentUser;
    private readonly ISpreadsheetService _spreadsheets;

    public StudentImportService(
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

        var groups = await _unitOfWork.StudentGroups.GetAllAsync(search: null, includeDrafts: true, cancellationToken);
        var names = groups.Select(group => group.Name).OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList();

        var template = new SpreadsheetTemplate(
            "Học sinh",
            [
                new TemplateColumn("Mã học sinh*", 16),
                new TemplateColumn("Họ và tên*", 30),
                new TemplateColumn("Lớp", 22, names.Count > 0 ? names : null),
                new TemplateColumn("Email", 30),
                new TemplateColumn("Số điện thoại", 16),
                new TemplateColumn("Tên đăng nhập", 20)
            ],
            [
                "Hướng dẫn nhập học sinh từ Excel",
                "Điền dữ liệu ở trang \"Học sinh\", bắt đầu từ dòng 2. Giữ nguyên hàng tiêu đề (dòng 1).",
                $"Mỗi lần nhập tối đa {MaxRows} dòng. Nhiều hơn thì tách thành nhiều file.",
                "Cột có dấu * là bắt buộc: Mã học sinh và Họ và tên. Các cột còn lại có thể để trống.",
                "Mã học sinh là khoá nhận diện và không được trùng. Mã đã có trong hệ thống thì dòng đó nói về học sinh cũ: hệ thống không tạo thêm tài khoản và không sửa hồ sơ, chỉ xếp thêm em vào lớp ghi trên dòng nếu em chưa học lớp đó.",
                "Lớp: chọn trong danh sách (trang \"Danh sách\"), phải là lớp đã có. Để trống nếu chọn lớp chung ở hộp nhập. Một học sinh học được nhiều lớp: ghi thêm lớp thì em được xếp thêm vào lớp đó, không rời lớp đang học. Lớp đã khoá không nhận thêm em.",
                "Email: để trống thì hệ thống tạo email tạm dạng ten-dang-nhap@" + PlaceholderEmailDomain + ". Hãy bổ sung email thật sau nếu cần gửi thư cho học sinh.",
                "Tên đăng nhập: để trống thì dùng chính mã học sinh. Phải từ 3 ký tự, không có khoảng trắng và chưa ai dùng.",
                "Số điện thoại: không bắt buộc, 9 đến 15 chữ số.",
                "Mật khẩu ban đầu: nhập một lần ở hộp nhập trên web, dùng chung cho cả đợt. Học sinh bắt buộc đổi mật khẩu ở lần đăng nhập đầu tiên.",
                "Dòng có lỗi sẽ được liệt kê (kèm số dòng và lý do), các dòng còn lại vẫn được nhập. Sửa xong có thể gửi lại nguyên file: dòng đã nhập rồi được nhận ra, không bị báo trùng."
            ],
            MaxRows);

        return Result.Success(_spreadsheets.BuildTemplate(template));
    }

    /// <inheritdoc />
    public Task<Result<ImportResultDto>> ImportAsync(
        Stream file,
        ImportStudentsOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        return ImportCoreAsync(() => _spreadsheets.Read(file, Columns, MaxRows), options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<ImportResultDto>> ImportRowsAsync(
        IReadOnlyList<SpreadsheetRow> rows,
        ImportStudentsOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return ImportCoreAsync(() => Result.Success(new SpreadsheetData(rows)), options, cancellationToken);
    }

    /// <summary>Một đường mã cho cả hai cách vào: đọc từ file, hoặc nhận sẵn các dòng (nhập tổng hợp dựng dòng từ một file duy nhất).</summary>
    private async Task<Result<ImportResultDto>> ImportCoreAsync(
        Func<Result<SpreadsheetData>> readData,
        ImportStudentsOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<ImportResultDto>(AdminOnlyMessage);
        }

        // Mật khẩu chỉ bắt buộc khi thật sự có tài khoản mới để tạo (xem bước ghi bên dưới): gửi lại file chỉ để xếp lớp cho
        // những em đã có tài khoản thì khỏi phải nghĩ ra mật khẩu. Nhưng nếu đã nhập thì báo sớm khi quá ngắn.
        var password = options.Password;
        if (!string.IsNullOrWhiteSpace(password) && password.Length < ValidationPatterns.MinPasswordLength)
        {
            return Result.Failure<ImportResultDto>(
                $"Mật khẩu ban đầu phải có ít nhất {ValidationPatterns.MinPasswordLength} ký tự.");
        }

        var groups = await _unitOfWork.StudentGroups.GetAllAsync(search: null, includeDrafts: true, cancellationToken);
        var groupByName = new Dictionary<string, StudentGroup>();
        foreach (var group in groups)
        {
            groupByName.TryAdd(ImportText.Fold(group.Name), group);
        }

        StudentGroup? defaultGroup = null;
        if (options.DefaultGroupId is { } defaultGroupId)
        {
            defaultGroup = groups.FirstOrDefault(group => group.Id == defaultGroupId);
            if (defaultGroup is null)
            {
                return Result.Failure<ImportResultDto>("Lớp đã chọn không tồn tại.");
            }

            if (defaultGroup.IsLocked)
            {
                return Result.Failure<ImportResultDto>(
                    $"Lớp {defaultGroup.Name} đã khoá nên không xếp thêm em vào được. Hãy mở khoá lớp hoặc chọn lớp khác.");
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
            if (TryParse(row, groupByName, defaultGroup, out var item, out var column, out var error))
            {
                parsed.Add(item!);
            }
            else
            {
                issues.Reject(row.Number, column, error!);
            }
        }

        // ── 2. Trùng nhau ngay trong file ───────────────────────────────────────────────────────
        // Mã, email và tên đăng nhập dùng chung MỘT không gian tên (đăng nhập tra cả ba cột), nên một mã trùng email của dòng
        // khác cũng là trùng. Dòng đầu tiên giữ định danh, các dòng sau bị từ chối.
        var firstSeen = new Dictionary<string, (int Row, string Field)>();
        var unique = new List<ParsedRow>();
        foreach (var item in parsed)
        {
            var clash = FindClash(item, firstSeen);
            if (clash is { } found)
            {
                issues.Reject(item.Row, found.Column, $"{found.Field} trùng với dòng {found.Row} trong file.");
                continue;
            }

            unique.Add(item);
        }

        // ── 3. Đối chiếu với tài khoản đã có ────────────────────────────────────────────────────
        // Email tạm của em không khai email cũng phải tra: một tài khoản cũ đã đổi tên đăng nhập vẫn có thể còn giữ đúng email tạm đó,
        // và để lọt thì cả mẻ vỡ ở ràng buộc duy nhất lúc ghi.
        var identifiers = unique
            .SelectMany(item => new[] { item.Code, item.Username, item.Email ?? PlaceholderEmail(item.Username) })
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!.ToLowerInvariant())
            .Distinct()
            .ToList();

        var existing = await _unitOfWork.Users.FindByIdentifiersAsync(identifiers, cancellationToken);
        var byCode = existing.Where(user => user.Code is not null).ToLookup(user => user.Code!.ToLowerInvariant());
        var taken = new HashSet<string>(
            existing.SelectMany(user => new[] { user.Email, user.Username, user.Code })
                .Where(value => !string.IsNullOrEmpty(value))
                .Select(value => value!.ToLowerInvariant()));

        var toCreate = new List<ParsedRow>();
        var toPlace = new List<(User Student, StudentGroup Target, ParsedRow Row)>();
        var unchanged = 0;

        foreach (var item in unique)
        {
            var student = byCode[item.Code.ToLowerInvariant()].FirstOrDefault();

            if (student is not null)
            {
                if (Classify(student, item) is { } issue)
                {
                    issues.Reject(item.Row, issue.Column, issue.Message);
                }
                else if (item.Target is null)
                {
                    // Không có lớp trong dòng thì không còn gì để làm: hồ sơ không bao giờ bị ghi đè từ file.
                    unchanged++;
                }
                else
                {
                    toPlace.Add((student, item.Target, item));
                }

                continue;
            }

            // Tài khoản mới: ba định danh đều phải còn trống.
            if (taken.Contains(item.Code.ToLowerInvariant()))
            {
                issues.Reject(item.Row, "Mã học sinh", $"Mã “{item.Code}” đang là định danh của một tài khoản khác (không phải học sinh).");
                continue;
            }

            if (taken.Contains(item.Username.ToLowerInvariant()))
            {
                issues.Reject(item.Row, "Tên đăng nhập", $"Tên đăng nhập “{item.Username}” đã có người dùng.");
                continue;
            }

            if (item.Email is not null && taken.Contains(item.Email.ToLowerInvariant()))
            {
                issues.Reject(item.Row, "Email", $"Email “{item.Email}” đã có người dùng.");
                continue;
            }

            toCreate.Add(item);
        }

        // Em đã có tài khoản và dòng ghi lớp: em đã học lớp đó thì không còn gì để làm, chưa học thì được xếp thêm vào (em học
        // được nhiều lớp cùng lúc nên không rời lớp nào). Mỗi lớp đích hỏi một lần cho cả mẻ.
        var placeable = new List<(User Student, StudentGroup Target, ParsedRow Row)>();
        foreach (var byTarget in toPlace.GroupBy(entry => entry.Target.Id))
        {
            var alreadyIn = await _unitOfWork.StudentGroups.GetMemberIdsAsync(
                byTarget.Key,
                byTarget.Select(entry => entry.Student.Id),
                cancellationToken);

            foreach (var entry in byTarget)
            {
                if (alreadyIn.Contains(entry.Student.Id))
                {
                    unchanged++;
                }
                else
                {
                    placeable.Add(entry);
                }
            }
        }

        // Email tạm cho học sinh không có email, chọn sau cùng để không đụng email thật của dòng nào khác.
        var reserved = new HashSet<string>(taken);
        foreach (var item in toCreate.Where(item => item.Email is not null))
        {
            reserved.Add(item.Email!.ToLowerInvariant());
        }

        var createPlans = new List<(ParsedRow Row, string Email)>();
        foreach (var item in toCreate)
        {
            createPlans.Add((item, item.Email ?? ReservePlaceholderEmail(item.Username, reserved)));
        }

        var failed = issues.FailedRows;
        var (shownIssues, truncated) = issues.Snapshot();

        if (options.DryRun || (createPlans.Count == 0 && placeable.Count == 0))
        {
            return Result.Success(new ImportResultDto
            {
                DryRun = options.DryRun,
                TotalRows = rows.Count,
                Created = createPlans.Count,
                Placed = placeable.Count,
                Unchanged = unchanged,
                Failed = failed,
                Issues = shownIssues,
                IssuesTruncated = truncated,
                Accounts = []
            });
        }

        // ── 4. Ghi một lần ──────────────────────────────────────────────────────────────────────
        if (createPlans.Count > 0 && string.IsNullOrWhiteSpace(password))
        {
            return Result.Failure<ImportResultDto>("Hãy nhập mật khẩu ban đầu cho các tài khoản mới.");
        }

        // Băm một lần cho cả đợt. Một lần SaveChanges là một giao dịch: hoặc cả mẻ vào, hoặc không em nào vào.
        var now = DateTime.UtcNow;
        var actorId = _currentUser.UserId;
        var passwordHash = createPlans.Count > 0 ? _passwordHasher.Hash(password!) : string.Empty;

        var newUsers = new List<User>(createPlans.Count);
        var newChanges = new List<AccountChange>(createPlans.Count + placeable.Count);

        foreach (var (item, email) in createPlans)
        {
            var student = new User
            {
                FullName = item.FullName,
                Email = email,
                Username = item.Username,
                Code = item.Code,
                Phone = item.Phone,
                PasswordHash = passwordHash,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                Role = UserRole.Student,
                Status = UserStatus.Active,
                AvatarColor = AvatarPalette.ForKey(item.Username),

                // §5: mật khẩu do quản trị viên cấp thì ép đổi ở lần đăng nhập đầu.
                MustChangePassword = true
            };

            newUsers.Add(student);

            if (item.Target is { } target)
            {
                student.GroupMemberships.Add(new StudentGroupMember { StudentGroupId = target.Id, JoinedAt = now });
                newChanges.Add(NewPlacement(student, target, actorId));
            }
        }

        foreach (var (student, target, _) in placeable)
        {
            _unitOfWork.StudentGroups.AddMember(
                new StudentGroupMember { StudentGroupId = target.Id, StudentId = student.Id, JoinedAt = now });
            newChanges.Add(NewPlacement(student, target, actorId));
        }

        await _unitOfWork.Users.AddRangeAsync(newUsers, cancellationToken);
        await _unitOfWork.AccountChanges.AddRangeAsync(newChanges, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ImportResultDto
        {
            DryRun = false,
            TotalRows = rows.Count,
            Created = newUsers.Count,
            Placed = placeable.Count,
            Unchanged = unchanged,
            Failed = failed,
            Issues = shownIssues,
            IssuesTruncated = truncated,
            Accounts = createPlans
                .Select(plan => new ImportedAccountDto
                {
                    FullName = plan.Row.FullName,
                    Username = plan.Row.Username,
                    Code = plan.Row.Code,
                    Group = plan.Row.Target?.Name
                })
                .ToList()
        });
    }

    /// <summary>Một dòng đã đọc và chuẩn hoá, chưa đối chiếu với dữ liệu đang có.</summary>
    private sealed record ParsedRow(
        int Row,
        string Code,
        string FullName,
        string Username,
        string? Email,
        string? Phone,
        StudentGroup? Target);

    private static bool TryParse(
        SpreadsheetRow row,
        IReadOnlyDictionary<string, StudentGroup> groupByName,
        StudentGroup? defaultGroup,
        out ParsedRow? parsed,
        out string? column,
        out string? error)
    {
        parsed = null;
        column = null;
        error = null;

        string Cell(string key) => row.Cells.TryGetValue(key, out var value) ? value : string.Empty;

        var code = ImportText.Collapse(Cell("code"));
        var fullName = ImportText.Collapse(Cell("fullName"));

        if (code.Length == 0)
        {
            column = "Mã học sinh";
            error = "Chưa có mã học sinh.";
            return false;
        }

        if (code.Length > MaxCodeLength)
        {
            column = "Mã học sinh";
            error = $"Mã học sinh dài quá {MaxCodeLength} ký tự.";
            return false;
        }

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

        var rawUsername = ImportText.Collapse(Cell("username"));
        var explicitUsername = rawUsername.Length > 0;
        var username = explicitUsername ? rawUsername : code;

        if (username.Length < MinUsernameLength
            || username.Length > MaxUsernameLength
            || username.Any(char.IsWhiteSpace))
        {
            column = explicitUsername ? "Tên đăng nhập" : "Mã học sinh";
            error = explicitUsername
                ? $"Tên đăng nhập phải từ {MinUsernameLength} đến {MaxUsernameLength} ký tự và không có khoảng trắng."
                : $"Mã học sinh dùng làm tên đăng nhập phải từ {MinUsernameLength} ký tự và không có khoảng trắng — hãy ghi tên đăng nhập riêng.";
            return false;
        }

        var email = ImportText.Collapse(Cell("email"));
        if (email.Length > 0)
        {
            if (email.Length > MaxEmailLength || !ValidationPatterns.Email.IsMatch(email))
            {
                column = "Email";
                error = "Email không đúng định dạng.";
                return false;
            }
        }

        var phone = ImportText.Phone(Cell("phone"));
        if (phone.Length > 0 && !ValidationPatterns.Phone.IsMatch(phone))
        {
            column = "Số điện thoại";
            error = "Số điện thoại không hợp lệ (9 đến 15 chữ số).";
            return false;
        }

        var groupName = ImportText.Collapse(Cell("group"));
        var target = defaultGroup;
        if (groupName.Length > 0)
        {
            if (!groupByName.TryGetValue(ImportText.Fold(groupName), out target))
            {
                column = "Lớp";
                error = $"Lớp “{groupName}” chưa có. Tạo lớp trước rồi nhập lại, hoặc để trống ô Lớp.";
                return false;
            }
        }

        if (target is { IsLocked: true })
        {
            column = "Lớp";
            error = $"Lớp “{target.Name}” đã khoá nên không xếp thêm em vào được. Mở khoá lớp trước, hoặc để trống ô Lớp.";
            return false;
        }

        parsed = new ParsedRow(
            row.Number,
            code,
            fullName,
            username,
            email.Length > 0 ? email : null,
            phone.Length > 0 ? phone : null,
            target);

        return true;
    }

    /// <summary>
    /// Ghi nhận ba định danh của một dòng vào bảng "đã thấy"; trả cột bị trùng nếu một định danh đã thuộc dòng khác.
    /// Định danh trùng <b>chính dòng đó</b> (tên đăng nhập mặc định bằng mã) không phải là trùng.
    /// </summary>
    private static (int Row, string Field, string Column)? FindClash(
        ParsedRow item,
        Dictionary<string, (int Row, string Field)> firstSeen)
    {
        var candidates = new List<(string Value, string Field)>
        {
            (item.Code, "Mã học sinh")
        };

        if (!string.Equals(item.Username, item.Code, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add((item.Username, "Tên đăng nhập"));
        }

        if (item.Email is not null)
        {
            candidates.Add((item.Email, "Email"));
        }

        foreach (var (value, field) in candidates)
        {
            if (firstSeen.TryGetValue(value.ToLowerInvariant(), out var seen) && seen.Row != item.Row)
            {
                return (seen.Row, field, field);
            }
        }

        foreach (var (value, field) in candidates)
        {
            firstSeen[value.ToLowerInvariant()] = (item.Row, field);
        }

        return null;
    }

    /// <summary>Đối chiếu một dòng với học sinh đã có cùng mã.</summary>
    private static (string Column, string Message)? Classify(User student, ParsedRow item)
    {
        if (student.Role != UserRole.Student)
        {
            return ("Mã học sinh", $"Mã “{item.Code}” đang là định danh của một tài khoản khác (không phải học sinh).");
        }

        // Cùng mã mà khác hẳn họ tên: gần như chắc chắn là gõ nhầm mã. Không đoán — báo để người nhập kiểm tra.
        if (ImportText.Fold(student.FullName) != ImportText.Fold(item.FullName))
        {
            return ("Mã học sinh", $"Mã “{item.Code}” đã thuộc học sinh {student.FullName}. Kiểm tra lại mã hoặc họ tên.");
        }

        if (student.Status is UserStatus.PendingDeletion or UserStatus.Deleted)
        {
            return ("Mã học sinh", $"Tài khoản của {student.FullName} đang chờ xoá hoặc đã xoá. Khôi phục tài khoản trước khi xếp lớp.");
        }

        return null;
    }

    private static string PlaceholderEmail(string username) => $"{username.ToLowerInvariant()}@{PlaceholderEmailDomain}";

    private static string ReservePlaceholderEmail(string username, HashSet<string> reserved)
    {
        var local = username.ToLowerInvariant();
        var candidate = PlaceholderEmail(username);

        for (var counter = 2; !reserved.Add(candidate.ToLowerInvariant()); counter++)
        {
            candidate = $"{local}-{counter}@{PlaceholderEmailDomain}";
        }

        return candidate;
    }

    private static AccountChange NewPlacement(User student, StudentGroup target, int? actorId) =>
        new()
        {
            // Qua navigation để khoá ngoại tự điền khi tài khoản mới được lưu; với tài khoản đã có thì Id đã sẵn.
            Account = student,
            Kind = AccountChangeKind.AddedToGroup,
            ToGroupId = target.Id,
            ToGroupName = target.Name,
            ActorUserId = actorId
        };
}
