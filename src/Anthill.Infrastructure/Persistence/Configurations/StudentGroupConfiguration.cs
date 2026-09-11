using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

public class StudentGroupConfiguration : IEntityTypeConfiguration<StudentGroup>
{
    public void Configure(EntityTypeBuilder<StudentGroup> builder)
    {
        builder.ToTable("StudentGroups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(g => g.Description)
            .HasMaxLength(512);

        // Xoá tài khoản người tạo không được kéo theo lớp — cắt luôn vòng khoá ngoại
        // Users -> StudentGroups -> Users.
        builder.HasOne(g => g.CreatedBy)
            .WithMany(u => u.CreatedGroups)
            .HasForeignKey(g => g.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tên lớp duy nhất trong TOÀN hệ thống, không phải duy nhất trong phạm vi một giáo viên.
        //
        // Trước đây lớp thuộc về từng giáo viên nên hai người được phép cùng có một lớp tên "12A1".
        // Giờ lớp là đơn vị hành chính của cả trung tâm: hai lớp trùng tên là hai thứ không ai phân
        // biệt nổi khi giao bài, và học sinh thì chỉ thuộc được một trong hai.
        builder.HasIndex(g => g.Name)
            .IsUnique()
            .HasDatabaseName("IX_StudentGroups_Name");

        // Lọc "lớp đã công bố" là truy vấn chạy ở mọi màn hình của giáo viên và học sinh.
        builder.HasIndex(g => g.IsPublished)
            .HasDatabaseName("IX_StudentGroups_IsPublished");
    }
}
