using EduQuiz.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduQuiz.Infrastructure.Persistence.Configurations;

public class AnswerOptionConfiguration : IEntityTypeConfiguration<AnswerOption>
{
    public void Configure(EntityTypeBuilder<AnswerOption> builder)
    {
        builder.ToTable("AnswerOptions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Content)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(a => a.ImageUrl)
            .HasMaxLength(512);

        // Sửa câu hỏi là thay cả mảng đáp án (§4.5), nên đáp án không sống sót qua câu hỏi.
        // Bài đã chấm không bị ảnh hưởng vì chúng chấm bằng ExamPaperAnswer, không phải bảng này.
        builder.HasOne(a => a.Question)
            .WithMany(q => q.Answers)
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.QuestionId, a.Ordinal })
            .HasDatabaseName("IX_AnswerOptions_Question_Ordinal");
    }
}
