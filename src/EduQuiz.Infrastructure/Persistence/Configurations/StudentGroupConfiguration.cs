using EduQuiz.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduQuiz.Infrastructure.Persistence.Configurations;

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

        // Xoá tài khoản giáo viên không được kéo theo nhóm lớp của họ — cắt luôn vòng
        // khoá ngoại Users -> StudentGroups -> Users.
        builder.HasOne(g => g.OwnerTeacher)
            .WithMany(u => u.OwnedGroups)
            .HasForeignKey(g => g.OwnerTeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(g => g.OwnerTeacherId)
            .HasDatabaseName("IX_StudentGroups_OwnerTeacherId");
    }
}
