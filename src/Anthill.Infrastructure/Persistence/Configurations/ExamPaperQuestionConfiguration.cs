using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

public class ExamPaperQuestionConfiguration : IEntityTypeConfiguration<ExamPaperQuestion>
{
    public void Configure(EntityTypeBuilder<ExamPaperQuestion> builder)
    {
        builder.ToTable("ExamPaperQuestions");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.ContentSnapshot)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(q => q.ImageUrlSnapshot)
            .HasMaxLength(512);

        builder.Property(q => q.Point)
            .HasPrecision(6, 2);

        builder.HasOne(q => q.ExamPaper)
            .WithMany(p => p.Questions)
            .HasForeignKey(q => q.ExamPaperId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chặn xoá câu hỏi đã nằm trong một đề đã sinh (§4.5). Khoá ngoại này chỉ để truy vết
        // nguồn — hiển thị và chấm điểm đều dùng snapshot ngay trong bảng này.
        builder.HasOne(q => q.Question)
            .WithMany(x => x.PaperQuestions)
            .HasForeignKey(q => q.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(q => new { q.ExamPaperId, q.Ordinal })
            .IsUnique()
            .HasDatabaseName("IX_ExamPaperQuestions_Paper_Ordinal");

        builder.HasIndex(q => q.QuestionId)
            .HasDatabaseName("IX_ExamPaperQuestions_QuestionId");
    }
}
