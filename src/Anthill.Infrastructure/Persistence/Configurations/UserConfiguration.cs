using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

/// <summary>
/// Một bảng dùng chung cho ba vai trò. Ba chỉ mục duy nhất ở đây là hàng rào cuối cùng
/// chống trùng danh tính (§5) — tầng service vẫn phải kiểm trước để trả 409 thay vì 500.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>Tên thuộc tính ẩn ánh xạ vào cột hệ thống <c>xmin</c>.</summary>
    public const string RowVersionProperty = "xmin";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(64);

        // Chuỗi băm BCrypt dài 60 ký tự; để dư cho trường hợp đổi thuật toán về sau.
        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(u => u.SecurityStamp)
            .IsRequired()
            .HasMaxLength(64);

        // Concurrency token là cột hệ thống xmin của PostgreSQL: mỗi lần dòng bị ghi, kể cả bởi SET NULL
        // của khoá ngoại, nó nhận mã transaction mới. EF thêm "AND xmin = <giá trị lúc đọc>" vào mọi câu
        // UPDATE, nên một request ghi lên bản đã cũ nhận DbUpdateConcurrencyException (409) thay vì âm
        // thầm ghi đè. Không thêm cột nào, và bắt được cả những lần ghi không đụng tới SecurityStamp —
        // khôi phục tài khoản chen giữa lúc tiến trình xoá đọc và lúc nó ghi là ví dụ.
        //
        // Thuộc tính ẩn (shadow) để tầng Domain không mang một chi tiết riêng của PostgreSQL.
        builder.Property<uint>(RowVersionProperty)
            .IsRowVersion();

        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(u => u.Status)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(u => u.StatusBeforeDeletion)
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(u => u.Code)
            .HasMaxLength(32);

        builder.Property(u => u.Phone)
            .HasMaxLength(32);

        builder.Property(u => u.AvatarColor)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(512);

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        builder.HasIndex(u => u.Username)
            .IsUnique()
            .HasDatabaseName("IX_Users_Username");

        // Mã học sinh chỉ tồn tại với vai trò Student. Chỉ mục lọc để hàng nghìn bản ghi
        // NULL của admin/teacher không phải nằm trong cây chỉ mục.
        builder.HasIndex(u => u.Code)
            .IsUnique()
            .HasFilter("\"Code\" IS NOT NULL")
            .HasDatabaseName("IX_Users_Code");

        builder.Property(u => u.DeletionReason)
            .HasMaxLength(512);

        // Người yêu cầu xoá là một tài khoản khác trong cùng bảng. Restrict để xoá tài khoản quản
        // trị viên không kéo theo việc mất dấu vết ai đã ra quyết định.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(u => u.DeletionRequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tiến trình nền quét theo đúng hai cột này mỗi lượt: ai đang chờ, và chờ từ bao giờ.
        builder.HasIndex(u => new { u.Status, u.DeletionRequestedAt })
            .HasDatabaseName("IX_Users_Status_DeletionRequestedAt");

        builder.HasIndex(u => u.Role)
            .HasDatabaseName("IX_Users_Role");

        // Xoá một môn không được kéo theo giáo viên của môn đó.
        builder.HasOne(u => u.Subject)
            .WithMany(s => s.Teachers)
            .HasForeignKey(u => u.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
