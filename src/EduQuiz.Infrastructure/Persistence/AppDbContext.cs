using EduQuiz.Domain.Common;
using EduQuiz.Domain.Entities;
using EduQuiz.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace EduQuiz.Infrastructure.Persistence;

/// <summary>
/// Phiên làm việc với PostgreSQL. Toàn bộ cấu hình ánh xạ nằm ở
/// <c>Persistence/Configurations</c>, mỗi entity một file.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>Tên cột <c>timestamptz</c> của PostgreSQL — mọi mốc thời gian đều là UTC (§1).</summary>
    private const string TimestampTz = "timestamp with time zone";

    private static readonly UtcDateTimeConverter UtcConverter = new();
    private static readonly NullableUtcDateTimeConverter NullableUtcConverter = new();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<User> Users => Set<User>();

    public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();

    public DbSet<Exam> Exams => Set<Exam>();

    public DbSet<ExamAssignment> ExamAssignments => Set<ExamAssignment>();

    public DbSet<ExamPaper> ExamPapers => Set<ExamPaper>();

    public DbSet<ExamPaperQuestion> ExamPaperQuestions => Set<ExamPaperQuestion>();

    public DbSet<ExamPaperAnswer> ExamPaperAnswers => Set<ExamPaperAnswer>();

    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();

    public DbSet<StudentAnswer> StudentAnswers => Set<StudentAnswer>();

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tìm kiếm tiếng Việt bỏ dấu (§5) dựa trên hàm unaccent() của PostgreSQL.
        // Khai báo ở đây để migration tự phát sinh "CREATE EXTENSION IF NOT EXISTS unaccent".
        modelBuilder.HasPostgresExtension("unaccent");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplyUtcDateTimeConvention(modelBuilder);
    }

    /// <summary>
    /// Đặt <c>CreatedAt</c> lúc thêm mới và <c>UpdatedAt</c> lúc sửa, ngay tại tầng hạ tầng.
    /// §5 yêu cầu <c>createdAt</c> do server đặt chứ không nhận từ client; làm ở đây thì không
    /// service nào có thể quên.
    /// </summary>
    /// <remarks>
    /// Ghi đè bản có <paramref name="acceptAllChangesOnSuccess"/> chứ không phải bản một tham số:
    /// mọi nạp chồng khác của <c>SaveChanges</c> đều gọi vào đây, nên không có đường ghi nào lách qua.
    /// </remarks>
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void StampTimestamps()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // BaseEntity đã gán sẵn UtcNow ở constructor; chỉ chữa lại khi bị bỏ trống.
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = utcNow;
                    }

                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = utcNow;
                    // CreatedAt là bất biến — chặn mọi lời gọi vô tình ghi đè.
                    entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                    break;
            }
        }
    }

    /// <summary>
    /// Gắn <c>timestamptz</c> và bộ chuyển UTC cho <b>mọi</b> thuộc tính ngày giờ trong model.
    /// Làm tập trung một chỗ để không có entity nào lọt lưới khi thêm cột mới về sau.
    /// </summary>
    private static void ApplyUtcDateTimeConvention(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(UtcConverter);
                    property.SetColumnType(TimestampTz);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(NullableUtcConverter);
                    property.SetColumnType(TimestampTz);
                }
            }
        }
    }
}
