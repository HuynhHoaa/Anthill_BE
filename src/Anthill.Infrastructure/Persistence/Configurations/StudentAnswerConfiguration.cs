using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

public class StudentAnswerConfiguration : IEntityTypeConfiguration<StudentAnswer>
{
    public void Configure(EntityTypeBuilder<StudentAnswer> builder)
    {
        builder.ToTable("StudentAnswers");

        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.Attempt)
            .WithMany(a => a.Answers)
            .HasForeignKey(s => s.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.ExamPaperQuestion)
            .WithMany(q => q.StudentAnswers)
            .HasForeignKey(s => s.ExamPaperQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tập id ExamPaperAnswer đã chọn (bản snapshot, không phải AnswerOption gốc). Mảng nguyên của
        // PostgreSQL: câu nhiều đáp án đúng ghi cả tập trong một dòng, nên unique (lượt, câu) bên dưới vẫn đúng.
        builder.Property(s => s.SelectedAnswerIds)
            .HasColumnType("integer[]")
            .IsRequired();

        // Lưu nháp mang ngữ nghĩa MERGE theo questionId (§4.7): mỗi câu trong một lượt làm bài
        // chỉ có đúng một dòng, autosave lần sau ghi đè lên chính dòng đó.
        builder.HasIndex(s => new { s.AttemptId, s.ExamPaperQuestionId })
            .IsUnique()
            .HasDatabaseName("IX_StudentAnswers_Attempt_Question");
    }
}
