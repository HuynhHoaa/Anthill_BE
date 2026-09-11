using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

public class ExamAttemptConfiguration : IEntityTypeConfiguration<ExamAttempt>
{
    public void Configure(EntityTypeBuilder<ExamAttempt> builder)
    {
        builder.ToTable("ExamAttempts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(a => a.Score)
            .HasPrecision(6, 2);

        builder.Property(a => a.MaxScore)
            .HasPrecision(6, 2);

        // Một assignment tối đa một lượt làm bài — v1 không cho làm lại. Ràng buộc 1-1 ở đây
        // cũng là thứ khiến "bắt đầu làm bài" idempotent trước cả khi service kịp kiểm (§4.7).
        builder.HasOne(a => a.Assignment)
            .WithOne(x => x.Attempt)
            .HasForeignKey<ExamAttempt>(a => a.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.AssignmentId)
            .IsUnique()
            .HasDatabaseName("IX_ExamAttempts_AssignmentId");

        // Background job quét bài quá hạn để tự chấm và bài sắp hết hạn để cảnh báo (§3 quy tắc ③).
        builder.HasIndex(a => new { a.Status, a.ExpiresAt })
            .HasDatabaseName("IX_ExamAttempts_Status_ExpiresAt");

        builder.HasIndex(a => a.SubmittedAt)
            .HasDatabaseName("IX_ExamAttempts_SubmittedAt");
    }
}
