using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

/// <summary>
/// Đề lưu dưới dạng blueprint. Cố ý <b>không</b> có cột <c>Lifecycle</c>: ba trạng thái
/// Scheduled · Open · Closed suy từ <c>OpenAt</c>/<c>CloseAt</c> so với giờ server (§5).
/// </summary>
public class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("Exams");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.Topic)
            .HasMaxLength(128);

        builder.Property(e => e.PointEasy)
            .HasPrecision(6, 2);

        builder.Property(e => e.PointMedium)
            .HasPrecision(6, 2);

        builder.Property(e => e.PointHard)
            .HasPrecision(6, 2);

        // Cùng độ chính xác với cột điểm tối đa của lượt làm bài, nên một danh sách ghi được là một danh sách
        // làm bài được.
        builder.Property(e => e.ListTotalScore)
            .HasPrecision(6, 2);

        // Đề có từ trước tính năng chia đề là đề một bản chung.
        builder.Property(e => e.PaperCount)
            .HasDefaultValue(1);

        builder.Property(e => e.PaperBonusUnits)
            .HasDefaultValue(0);

        // Suy ra từ hai cột trên, không có cột riêng.
        builder.Ignore(e => e.UsesPerAssignmentPapers);

        builder.HasOne(e => e.CreatedBy)
            .WithMany(u => u.CreatedExams)
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CreatedByUserId)
            .HasDatabaseName("IX_Exams_CreatedByUserId");

        // Bộ lọc vòng đời luôn là (IsDraft, OpenAt, CloseAt) so với một mốc thời gian truyền vào.
        builder.HasIndex(e => new { e.IsDraft, e.OpenAt, e.CloseAt })
            .HasDatabaseName("IX_Exams_Lifecycle");
    }
}
