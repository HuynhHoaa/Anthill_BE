using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(n => n.Link)
            .HasMaxLength(512);

        builder.Property(n => n.TargetType)
            .HasMaxLength(64);

        builder.HasOne(n => n.Recipient)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Truy vấn duy nhất của màn hình thông báo: của tôi, chưa đọc hoặc tất cả, mới nhất trước.
        builder.HasIndex(n => new { n.RecipientUserId, n.IsRead, n.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_Notifications_Recipient_IsRead_CreatedAt");
    }
}
