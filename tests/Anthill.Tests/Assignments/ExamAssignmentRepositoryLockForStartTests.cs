using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using Anthill.Infrastructure.Persistence;
using Anthill.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Tests.Assignments;

/// <summary>
/// H11: <c>LockForStartAsync</c> phải dùng <c>FOR UPDATE</c> để hai request "Vào thi" cùng lúc trên
/// CÙNG một dòng giao bài xếp hàng, thay vì cùng đọc "chưa có lượt nào" rồi cùng đi tới INSERT (
/// <c>FOR SHARE</c> cũ không loại trừ chính nó — hai khoá chia sẻ cùng tồn tại được — xem remarks của
/// <c>LockForStartAsync</c>).
/// </summary>
/// <remarks>
/// <para>
/// Đây là hành vi của chính PostgreSQL, không phải của C#, nên chỉ gọi hàm hai lần nối tiếp trong cùng
/// một transaction (hay hai transaction không thật sự chạy đồng thời) không chứng minh được gì về lock
/// mode — cả <c>FOR SHARE</c> lẫn <c>FOR UPDATE</c> đều cho qua một lần gọi tuần tự. Test dưới đây mở
/// hai <see cref="AppDbContext"/> riêng biệt (mô phỏng hai HTTP request, mỗi request một DbContext theo
/// đúng vòng đời scoped của ASP.NET Core), mỗi bên một transaction thật, và đo xem giao dịch B có phải
/// CHỜ giao dịch A commit hay không — đúng câu hỏi mà H11 đặt ra.
/// </para>
/// <para>
/// Cần một PostgreSQL thật đã migrate đầy đủ, trỏ qua biến môi trường
/// <c>ConnectionStrings__DefaultConnection</c> — đúng biến mà <c>kit_api_start</c>/<c>kit_migrate</c> của
/// kit đã dùng để chạy API. Không có biến này (ví dụ ai đó gọi <c>dotnet test</c> trần, không qua kit)
/// thì test tự bỏ qua: phần còn lại của bộ kiểm toàn hàm thuần không chạm database (xem
/// <c>AssignmentSchedule.cs</c>), và bắt buộc phải có Postgres cho một lần <c>dotnet test</c> bình
/// thường sẽ làm hỏng lần chạy của bất kỳ ai không mở sẵn database thật.
/// </para>
/// </remarks>
public sealed class ExamAssignmentRepositoryLockForStartTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

    private static AppDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    /// <summary>Một giáo viên, một học sinh, một đề, một dòng giao bài — vừa đủ cho LockForStartAsync.</summary>
    private static async Task<int> SeedAssignmentAsync(AppDbContext context)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];

        var teacher = new User
        {
            FullName = "GV Test",
            Email = $"gv-{suffix}@test.local",
            Username = $"gv-{suffix}",
            PasswordHash = "x",
            SecurityStamp = "x",
            AvatarColor = "#000000",
            Role = UserRole.Teacher
        };
        var student = new User
        {
            FullName = "HS Test",
            Email = $"hs-{suffix}@test.local",
            Username = $"hs-{suffix}",
            PasswordHash = "x",
            SecurityStamp = "x",
            AvatarColor = "#000000",
            Role = UserRole.Student,
            Code = $"HS-{suffix}"
        };
        context.Users.AddRange(teacher, student);
        await context.SaveChangesAsync();

        var exam = new Exam
        {
            Title = $"De test khoa {suffix}",
            DurationMinutes = 30,
            TotalQuestions = 10,
            EasyPercentage = 100,
            MediumPercentage = 0,
            HardPercentage = 0,
            PointEasy = 1,
            PointMedium = 1,
            PointHard = 1,
            IsDraft = false,
            OpenAt = DateTime.UtcNow.AddMinutes(-10),
            CloseAt = DateTime.UtcNow.AddHours(1),
            CreatedByUserId = teacher.Id
        };
        context.Exams.Add(exam);
        await context.SaveChangesAsync();

        var assignment = new ExamAssignment
        {
            ExamId = exam.Id,
            StudentId = student.Id,
            AssignedAt = DateTime.UtcNow
        };
        context.ExamAssignments.Add(assignment);
        await context.SaveChangesAsync();

        return assignment.Id;
    }

    [Fact]
    public async Task LockForStartAsync_request_thu_hai_phai_cho_request_dau_commit_khong_duoc_doc_song_song()
    {
        var connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var seedContext = NewContext(connectionString);
        var assignmentId = await SeedAssignmentAsync(seedContext);

        // A: "Vào thi" tới trước, cầm khoá và CHƯA commit — mô phỏng transaction đang xử lý giữa chừng
        // (sinh đề, tính ExpiresAt...) của CreateAttemptAsync.
        await using var contextA = NewContext(connectionString);
        await using var transactionA = await contextA.Database.BeginTransactionAsync();
        var repositoryA = new ExamAssignmentRepository(contextA);

        var lockedA = await repositoryA.LockForStartAsync(assignmentId);
        lockedA.Should().NotBeNull();

        // B: tab thứ hai của cùng em (hay React StrictMode, double-click, retry) bấm "Vào thi" gần như
        // cùng lúc — chạy trên một DbContext và một transaction RIÊNG, y hệt một HTTP request khác.
        await using var contextB = NewContext(connectionString);
        var repositoryB = new ExamAssignmentRepository(contextB);

        var lockTaskB = Task.Run(async () =>
        {
            await using var transactionB = await contextB.Database.BeginTransactionAsync();
            var lockedB = await repositoryB.LockForStartAsync(assignmentId);
            await transactionB.CommitAsync();
            return lockedB;
        });

        // Đây chính là điều FOR SHARE (cũ) và FOR UPDATE (mới) trả lời khác nhau: FOR SHARE cho B đọc
        // xong ngay lập tức, song song với A — đúng lỗ hổng H11. FOR UPDATE bắt B chờ tới khi A commit
        // hoặc rollback. Nửa giây là dư dả cho một SELECT ... FOR UPDATE không bị chặn hoàn tất.
        var finishedEarly = await Task.WhenAny(lockTaskB, Task.Delay(TimeSpan.FromMilliseconds(500)));
        finishedEarly.Should().NotBe(lockTaskB, "B phải bị chặn chờ A commit, không được đọc song song với A");

        await transactionA.CommitAsync();

        // Giờ A đã commit và nhả khoá, B phải hoàn tất nhanh chóng — và không sinh ra một lỗi/deadlock nào.
        var lockedB = await lockTaskB.WaitAsync(TimeSpan.FromSeconds(5));
        lockedB.Should().NotBeNull();
        lockedB!.Id.Should().Be(assignmentId);
    }
}
