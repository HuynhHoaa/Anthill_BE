using Anthill.Application.Interfaces.Files;

namespace Anthill.Application.Common;

/// <summary>Một giáo viên chủ nhiệm cần tạo, dựng từ các dòng học sinh. <see cref="FirstRow"/> là dòng đầu tiên nhắc tới giáo viên này.</summary>
public sealed record PlannedTeacher(string Email, int FirstRow, SpreadsheetRow Row);

/// <summary>Một lớp cần tạo, kèm email giáo viên chủ nhiệm. <see cref="FirstRow"/> là dòng đầu tiên của lớp trong file.</summary>
public sealed record PlannedGroup(string Key, string Name, int FirstRow, string TeacherEmail, SpreadsheetRow Row, string Term);

/// <summary>Một học sinh cần nhập, đã biết lớp (qua <see cref="GroupKey"/>).</summary>
public sealed record PlannedStudent(int Row, string GroupKey, SpreadsheetRow Source);

/// <summary>Một dòng của file bị từ chối ngay khi lập kế hoạch (trước khi chạm database).</summary>
public sealed record SchoolImportIssue(int Row, string? Column, string Message);

/// <summary>Kế hoạch nhập: ba danh sách dòng cho ba bộ nhập cũ, và các lỗi đã phát hiện từ cấu trúc file.</summary>
public sealed record SchoolImportPlan(
    IReadOnlyList<PlannedTeacher> Teachers,
    IReadOnlyList<PlannedGroup> Groups,
    IReadOnlyList<PlannedStudent> Students,
    IReadOnlyList<SchoolImportIssue> Issues);

/// <summary>
/// Từ một trang Excel duy nhất (mỗi dòng một học sinh, kèm lớp và giáo viên chủ nhiệm) dựng ra danh sách giáo viên, danh sách lớp
/// và danh sách học sinh để ba bộ nhập cũ xử lý. Thuần logic, không đụng database, nên kiểm bằng unit test được.
/// </summary>
/// <remarks>
/// <para>
/// <b>Giáo viên và lớp suy ra từ các dòng</b>: cùng tên lớp (không phân biệt hoa thường và dấu) là một lớp; cùng email giáo viên là
/// một người. Các dòng sau của cùng một lớp được <b>để trống</b> cột giáo viên (điền một lần ở dòng đầu hay điền lặp ở mọi dòng đều
/// được); chỉ khi hai ô cùng có giá trị mà khác nhau mới là xung đột và dòng đến sau bị từ chối.
/// </para>
/// <para>
/// Số dòng của mỗi bản ghi suy ra (giáo viên, lớp) là số dòng thật trong file ở lần xuất hiện đầu tiên, nên lỗi báo về từ ba bộ nhập
/// cũ trỏ đúng dòng của người dùng mà không cần đổi số dòng.
/// </para>
/// </remarks>
public static class SchoolImportPlanner
{
    public static SchoolImportPlan Build(IReadOnlyList<SpreadsheetRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var issues = new List<SchoolImportIssue>();
        var classes = new Dictionary<string, ClassAccumulator>();
        var teachers = new Dictionary<string, TeacherAccumulator>();
        var kept = new List<(SpreadsheetRow Row, string GroupKey)>();

        foreach (var row in rows)
        {
            var groupName = ImportText.Collapse(Cell(row, "group"));
            if (groupName.Length == 0)
            {
                issues.Add(new SchoolImportIssue(row.Number, "Lớp", "Chưa có lớp. Mỗi học sinh phải thuộc một lớp."));
                continue;
            }

            var groupKey = ImportText.Fold(groupName);
            if (!classes.TryGetValue(groupKey, out var group))
            {
                group = new ClassAccumulator(groupName, row.Number);
                classes[groupKey] = group;
            }

            // Kỳ học của lớp: ghi một lần ở một dòng của lớp (hoặc lặp lại giống hệt); hai kỳ khác nhau trong một lớp là xung đột.
            var term = ImportText.Collapse(Cell(row, "term"));
            if (term.Length > 0)
            {
                if (group.Term is null)
                {
                    group.Term = term;
                    group.TermRow = row.Number;
                }
                else if (ImportText.Fold(group.Term) != ImportText.Fold(term))
                {
                    issues.Add(new SchoolImportIssue(
                        row.Number,
                        "Kỳ học",
                        $"Lớp “{group.Name}” đã ghi kỳ học “{group.Term}” (dòng {group.TermRow}), dòng này lại ghi “{term}”."));
                    continue;
                }
            }

            var email = ImportText.Collapse(Cell(row, "teacherEmail")).ToLowerInvariant();
            if (email.Length > 0)
            {
                if (group.TeacherEmail is null)
                {
                    group.TeacherEmail = email;
                    group.TeacherEmailRow = row.Number;
                }
                else if (group.TeacherEmail != email)
                {
                    issues.Add(new SchoolImportIssue(
                        row.Number,
                        "Email giáo viên",
                        $"Lớp “{group.Name}” đã có giáo viên chủ nhiệm {group.TeacherEmail} (dòng {group.TeacherEmailRow}), dòng này lại ghi {email}."));
                    continue;
                }

                if (!teachers.TryGetValue(email, out var teacher))
                {
                    teacher = new TeacherAccumulator(email, row.Number);
                    teachers[email] = teacher;
                }

                if (!teacher.Merge(
                        row.Number,
                        ImportText.Collapse(Cell(row, "teacherName")),
                        ImportText.Phone(Cell(row, "teacherPhone")),
                        ImportText.Collapse(Cell(row, "subject")),
                        issues))
                {
                    continue;
                }
            }

            kept.Add((row, groupKey));
        }

        // Lớp mà mọi dòng đều bị từ chối (xung đột giáo viên...) thì không có học sinh nào để xếp: không tạo lớp rỗng chỉ vì nó được
        // nhắc tới trong một dòng hỏng.
        var keptKeys = kept.Select(item => item.GroupKey).ToHashSet();

        var plannedGroups = new List<PlannedGroup>();
        var blocked = new Dictionary<string, ClassAccumulator>();
        foreach (var (key, group) in classes.Where(entry => keptKeys.Contains(entry.Key)))
        {
            if (group.TeacherEmail is null)
            {
                issues.Add(new SchoolImportIssue(
                    group.FirstRow,
                    "Email giáo viên",
                    $"Lớp “{group.Name}” chưa có giáo viên chủ nhiệm. Điền email giáo viên ở ít nhất một dòng của lớp."));
                blocked[key] = group;
                continue;
            }

            plannedGroups.Add(new PlannedGroup(
                key,
                group.Name,
                group.FirstRow,
                group.TeacherEmail,
                new SpreadsheetRow(group.FirstRow, new Dictionary<string, string>
                {
                    ["name"] = group.Name,
                    ["teacher"] = group.TeacherEmail,
                    ["description"] = string.Empty,
                    ["term"] = group.Term ?? string.Empty
                }),
                group.Term ?? string.Empty));
        }

        var usedTeachers = plannedGroups.Select(group => group.TeacherEmail).ToHashSet();
        var plannedTeachers = teachers.Values
            .Where(teacher => usedTeachers.Contains(teacher.Email))
            .OrderBy(teacher => teacher.FirstRow)
            .Select(teacher => new PlannedTeacher(
                teacher.Email,
                teacher.FirstRow,
                new SpreadsheetRow(teacher.FirstRow, new Dictionary<string, string>
                {
                    ["fullName"] = teacher.Name,
                    ["email"] = teacher.Email,
                    ["phone"] = teacher.Phone,
                    ["subject"] = teacher.Subject
                })))
            .ToList();

        var plannedStudents = new List<PlannedStudent>();
        foreach (var (row, groupKey) in kept)
        {
            if (blocked.TryGetValue(groupKey, out var blockedGroup))
            {
                // Dòng gốc của lớp đã mang lỗi chính; chỉ các dòng còn lại cần được báo là chưa xếp được.
                if (row.Number != blockedGroup.FirstRow)
                {
                    issues.Add(new SchoolImportIssue(
                        row.Number,
                        "Lớp",
                        $"Lớp “{blockedGroup.Name}” chưa nhập được (xem dòng {blockedGroup.FirstRow}), nên học sinh này chưa được xếp."));
                }

                continue;
            }

            plannedStudents.Add(new PlannedStudent(
                row.Number,
                groupKey,
                new SpreadsheetRow(row.Number, new Dictionary<string, string>
                {
                    ["code"] = Cell(row, "code"),
                    ["fullName"] = Cell(row, "fullName"),
                    // Tên lớp ghi theo dạng chuẩn của lớp (lần xuất hiện đầu), để khớp lớp vừa tạo dù mỗi dòng gõ khác nhau.
                    ["group"] = classes[groupKey].Name,
                    ["email"] = Cell(row, "email"),
                    ["phone"] = Cell(row, "phone"),
                    ["username"] = Cell(row, "username")
                })));
        }

        return new SchoolImportPlan(plannedTeachers, plannedGroups, plannedStudents, issues);
    }

    private static string Cell(SpreadsheetRow row, string key) =>
        row.Cells.TryGetValue(key, out var value) ? value : string.Empty;

    private sealed class ClassAccumulator(string name, int firstRow)
    {
        public string Name { get; } = name;

        public int FirstRow { get; } = firstRow;

        public string? TeacherEmail { get; set; }

        public int TeacherEmailRow { get; set; }

        public string? Term { get; set; }

        public int TermRow { get; set; }
    }

    private sealed class TeacherAccumulator(string email, int firstRow)
    {
        public string Email { get; } = email;

        public int FirstRow { get; } = firstRow;

        public string Name { get; private set; } = string.Empty;

        public string Phone { get; private set; } = string.Empty;

        public string Subject { get; private set; } = string.Empty;

        /// <summary>So số điện thoại theo chữ số: "0912 345 678" và "0912345678" là một số.</summary>
        private static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

        /// <summary>Gộp thông tin giáo viên từ một dòng. <c>false</c> khi dòng này mâu thuẫn với điều đã ghi ở dòng trước (đã ghi lỗi).</summary>
        public bool Merge(int row, string name, string phone, string subject, List<SchoolImportIssue> issues)
        {
            var ok = true;

            if (name.Length > 0)
            {
                if (Name.Length == 0)
                {
                    Name = name;
                }
                else if (ImportText.Fold(Name) != ImportText.Fold(name))
                {
                    issues.Add(new SchoolImportIssue(
                        row,
                        "Giáo viên chủ nhiệm",
                        $"Email {Email} đã ghi tên “{Name}” (dòng {FirstRow}), dòng này ghi “{name}”."));
                    ok = false;
                }
            }

            if (phone.Length > 0)
            {
                if (Phone.Length == 0)
                {
                    Phone = phone;
                }
                else if (Digits(Phone) != Digits(phone))
                {
                    issues.Add(new SchoolImportIssue(
                        row,
                        "Số điện thoại giáo viên",
                        $"Email {Email} đã ghi số {Phone} (dòng {FirstRow}), dòng này ghi {phone}."));
                    ok = false;
                }
            }

            if (subject.Length > 0)
            {
                if (Subject.Length == 0)
                {
                    Subject = subject;
                }
                else if (ImportText.Fold(Subject) != ImportText.Fold(subject))
                {
                    issues.Add(new SchoolImportIssue(
                        row,
                        "Môn giảng dạy",
                        $"Email {Email} đã ghi môn “{Subject}” (dòng {FirstRow}), dòng này ghi “{subject}”."));
                    ok = false;
                }
            }

            return ok;
        }
    }
}
