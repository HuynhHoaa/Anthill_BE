using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

public class AccountChangeConfiguration : IEntityTypeConfiguration<AccountChange>
{
    public void Configure(EntityTypeBuilder<AccountChange> builder)
    {
        builder.ToTable("AccountChanges");

        builder.Property(c => c.Kind)
            .IsRequired()
            .HasMaxLength(24)
            .HasConversion<string>();

        // Cùng trần với lý do xoá tài khoản (Users.DeletionReason): mọi lý do quản trị viên nhập đều qua một bộ luật.
        builder.Property(c => c.Reason)
            .HasMaxLength(512);

        builder.Property(c => c.FromGroupName)
            .HasMaxLength(200);

        builder.Property(c => c.ToGroupName)
            .HasMaxLength(200);

        // Tài khoản không bao giờ bị xoá dòng (học sinh chỉ bị ẩn danh hoá, xem StudentAnonymizer; giáo viên chỉ bị khoá),
        // nên xoá nhầm một tài khoản mà kéo theo cả lịch sử của nó phải là điều database từ chối.
        builder.HasOne(c => c.Account)
            .WithMany()
            .HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Người thực hiện biến mất thì dòng lịch sử vẫn ở lại, chỉ mất tên.
        builder.HasOne(c => c.Actor)
            .WithMany()
            .HasForeignKey(c => c.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Mã lớp ở hai cột này cố ý KHÔNG có khoá ngoại: lớp trống bị xoá được, mà lịch sử thì phải còn.
        builder.HasIndex(c => new { c.AccountId, c.CreatedAt })
            .HasDatabaseName("IX_AccountChanges_AccountId_CreatedAt");
    }
}
