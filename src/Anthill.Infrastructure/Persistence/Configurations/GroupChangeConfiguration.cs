using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

public class GroupChangeConfiguration : IEntityTypeConfiguration<GroupChange>
{
    public void Configure(EntityTypeBuilder<GroupChange> builder)
    {
        builder.ToTable("GroupChanges");

        builder.Property(c => c.Kind)
            .IsRequired()
            .HasMaxLength(24)
            .HasConversion<string>();

        // Cùng trần với mọi lý do quản trị viên nhập (Users.DeletionReason, AccountChanges.Reason).
        builder.Property(c => c.Reason)
            .HasMaxLength(512);

        builder.Property(c => c.FromTeacherName)
            .HasMaxLength(128);

        builder.Property(c => c.ToTeacherName)
            .HasMaxLength(128);

        // Lịch sử thuộc về lớp: lớp trống bị xoá thì lịch sử của nó đi theo.
        builder.HasOne(c => c.Group)
            .WithMany()
            .HasForeignKey(c => c.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Người thực hiện biến mất thì dòng lịch sử vẫn ở lại, chỉ mất tên.
        builder.HasOne(c => c.Actor)
            .WithMany()
            .HasForeignKey(c => c.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Mã giáo viên ở hai cột này cố ý KHÔNG có khoá ngoại: tên được chép lại, dòng lịch sử phải đọc ra đúng như lúc đó.
        builder.HasIndex(c => new { c.GroupId, c.CreatedAt })
            .HasDatabaseName("IX_GroupChanges_GroupId_CreatedAt");
    }
}
