using Anthill.Application.Interfaces.Security;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Anthill.Infrastructure.Persistence.Seed;

/// <summary>
/// Dữ liệu tối thiểu để hệ thống chạy được ngay sau khi tạo database: một môn, ba tài khoản
/// đúng bằng ba tài khoản demo in trên trang đăng nhập của frontend, và một nhóm lớp.
/// </summary>
/// <remarks>
/// <para>
/// Toàn bộ thao tác đều <b>idempotent</b>: mỗi bước kiểm tra tồn tại trước khi thêm, nên chạy
/// bao nhiêu lần cũng ra cùng một trạng thái. Điều này quan trọng vì seeder sẽ được gọi ở mỗi
/// lần khởi động, còn dịch vụ miễn phí thì khởi động lại thường xuyên.
/// </para>
/// <para>
/// Cố ý <b>không</b> gọi <c>Database.Migrate()</c> ở đây: kế hoạch deploy tách migration thành
/// một bước riêng của pipeline để không kéo dài thời gian boot.
/// </para>
/// <para>
/// <b>Ba tài khoản này là dữ liệu demo.</b> §5 yêu cầu xoá sạch tài khoản mẫu và mật khẩu mẫu
/// trước khi lên production thật.
/// </para>
/// </remarks>
public static class DbSeeder
{
    /// <summary>Mật khẩu dùng chung của ba tài khoản demo.</summary>
    public const string DemoPassword = "123456";

    private const string DemoSchool = "Trường THPT Nguyễn Du";
    private const string SubjectCode = "KHTN";
    private const string SubjectName = "Khoa học tự nhiên";
    private const string GroupName = "Lớp 8A";

    private const string AdminEmail = "huy.pq@anthill.edu.vn";
    private const string TeacherEmail = "an.nv@anthill.edu.vn";
    private const string StudentEmail = "lan.tt@student.edu.vn";

    /// <summary>Tạo scope riêng rồi gọi bản chính — tiện gọi thẳng từ <c>Program.cs</c>.</summary>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await SeedAsync(context, passwordHasher, cancellationToken);
    }

    public static async Task SeedAsync(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(passwordHasher);

        var subject = await EnsureSubjectAsync(context, cancellationToken);

        var admin = await EnsureUserAsync(context, passwordHasher, BuildAdmin(), cancellationToken);
        var teacher = await EnsureUserAsync(context, passwordHasher, BuildTeacher(subject.Id), cancellationToken);

        // Ghi ngay để có Id thật trước khi lớp và học sinh trỏ vào hai tài khoản này.
        await context.SaveChangesAsync(cancellationToken);

        // Quản trị viên lập lớp, giáo viên được phân công dạy — đúng mô hình của trung tâm.
        var group = await EnsureGroupAsync(context, admin.Id, teacher.Id, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await EnsureUserAsync(context, passwordHasher, BuildStudent(group.Id), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Subject> EnsureSubjectAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var existing = await context.Subjects.FirstOrDefaultAsync(s => s.Code == SubjectCode, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var subject = new Subject { Name = SubjectName, Code = SubjectCode };
        await context.Subjects.AddAsync(subject, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return subject;
    }

    /// <summary>
    /// Lớp mẫu: do quản trị viên lập, đã công bố, và giáo viên demo được phân công dạy.
    /// </summary>
    /// <remarks>
    /// Dữ liệu demo phải thể hiện đúng mô hình thật, không phải một lối tắt cho nhanh. Nếu lớp mẫu
    /// để dạng nháp hoặc không phân công ai thì người mở thử sẽ đăng nhập bằng tài khoản giáo viên
    /// và thấy một màn hình trống rỗng — rồi kết luận là hệ thống hỏng.
    ///
    /// Tên lớp giờ duy nhất trên toàn hệ thống nên chỉ cần tìm theo tên.
    /// </remarks>
    private static async Task<StudentGroup> EnsureGroupAsync(
        AppDbContext context,
        int adminUserId,
        int teacherUserId,
        CancellationToken cancellationToken)
    {
        var existing = await context.StudentGroups
            .Include(g => g.Teachers)
            .FirstOrDefaultAsync(g => g.Name == GroupName, cancellationToken);

        if (existing is not null)
        {
            EnsureTeacherAssigned(context, existing, teacherUserId);
            return existing;
        }

        var group = new StudentGroup
        {
            Name = GroupName,
            Description = "Lớp mẫu dùng cho dữ liệu demo.",
            CreatedByUserId = adminUserId,
            IsPublished = true,
            PublishedAt = DateTime.UtcNow
        };

        await context.StudentGroups.AddAsync(group, cancellationToken);
        EnsureTeacherAssigned(context, group, teacherUserId);

        return group;
    }

    /// <summary>Phân công giáo viên vào lớp nếu chưa có, không nhân đôi khi chạy lại.</summary>
    private static void EnsureTeacherAssigned(AppDbContext context, StudentGroup group, int teacherUserId)
    {
        if (group.Teachers.Any(t => t.TeacherId == teacherUserId))
        {
            return;
        }

        group.Teachers.Add(new StudentGroupTeacher
        {
            StudentGroup = group,
            TeacherId = teacherUserId,
            AssignedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Nhận diện theo email vì đó là khoá tự nhiên của cả ba vai trò. Tài khoản đã có thì trả
    /// nguyên bản ghi cũ — không đụng vào mật khẩu, không reset trạng thái khoá.
    /// </summary>
    private static async Task<User> EnsureUserAsync(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        UserSeed seed,
        CancellationToken cancellationToken)
    {
        var existing = await context.Users.FirstOrDefaultAsync(u => u.Email == seed.Email, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        // Chỉ băm mật khẩu khi thật sự tạo mới: BCrypt work factor 12 tốn hàng trăm mili giây,
        // không đáng trả giá đó ở mỗi lần khởi động.
        var user = new User
        {
            FullName = seed.FullName,
            Email = seed.Email,
            Username = seed.Username,
            PasswordHash = passwordHasher.Hash(DemoPassword),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Role = seed.Role,
            Status = UserStatus.Active,
            SubjectId = seed.SubjectId,
            Code = seed.Code,
            GroupId = seed.GroupId,
            Phone = seed.Phone,
            School = DemoSchool,
            AvatarColor = seed.AvatarColor,

            // Tài khoản demo phải dùng được ngay, không chặn bằng màn hình đổi mật khẩu.
            MustChangePassword = false
        };

        await context.Users.AddAsync(user, cancellationToken);
        return user;
    }

    private static UserSeed BuildAdmin() => new()
    {
        FullName = "Phạm Quốc Huy",
        Email = AdminEmail,
        Username = "huy.pq",
        Role = UserRole.Admin,
        Phone = "0903 112 233",
        AvatarColor = "#0EA5E9"
    };

    private static UserSeed BuildTeacher(int subjectId) => new()
    {
        FullName = "Nguyễn Văn An",
        Email = TeacherEmail,
        Username = "an.nv",
        Role = UserRole.Teacher,
        SubjectId = subjectId,
        Phone = "0912 345 678",
        AvatarColor = "#6366F1"
    };

    private static UserSeed BuildStudent(int groupId) => new()
    {
        FullName = "Trần Thị Lan",
        Email = StudentEmail,
        Username = "lan.tt",
        Role = UserRole.Student,
        Code = "HS1001",
        GroupId = groupId,
        Phone = "0987 654 321",
        AvatarColor = "#8B5CF6"
    };

    private sealed record UserSeed
    {
        public required string FullName { get; init; }

        public required string Email { get; init; }

        public required string Username { get; init; }

        public required UserRole Role { get; init; }

        public required string AvatarColor { get; init; }

        public int? SubjectId { get; init; }

        public int? GroupId { get; init; }

        public string? Code { get; init; }

        public string? Phone { get; init; }
    }
}
