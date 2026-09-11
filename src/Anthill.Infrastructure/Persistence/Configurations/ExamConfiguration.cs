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

        builder.Property(e => e.PointPerQuestion)
            .HasPrecision(6, 2);

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
