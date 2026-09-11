using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

/// <summary>
/// Phương án trong đề đã sinh. Cột <c>IsCorrect</c> ở đây là nguồn sự thật duy nhất khi chấm —
/// không bao giờ tra ngược <see cref="AnswerOption"/> gốc vì nó có thể đã bị sửa (§5).
/// </summary>
public class ExamPaperAnswerConfiguration : IEntityTypeConfiguration<ExamPaperAnswer>
{
    public void Configure(EntityTypeBuilder<ExamPaperAnswer> builder)
    {
        builder.ToTable("ExamPaperAnswers");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ContentSnapshot)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(a => a.ImageUrlSnapshot)
            .HasMaxLength(512);

        builder.HasOne(a => a.ExamPaperQuestion)
            .WithMany(q => q.Answers)
            .HasForeignKey(a => a.ExamPaperQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.ExamPaperQuestionId, a.Ordinal })
            .IsUnique()
            .HasDatabaseName("IX_ExamPaperAnswers_Question_Ordinal");
    }
}
