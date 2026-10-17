using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

/// <summary>
/// Danh sách câu hỏi của một đề soạn theo danh sách. Khác <c>ExamPaperQuestions</c>: bảng này trỏ tới câu
/// hỏi đang sống trong ngân hàng, còn bản đề mới là bản chụp nội dung lúc giao bài.
/// </summary>
public class ExamQuestionConfiguration : IEntityTypeConfiguration<ExamQuestion>
{
    public void Configure(EntityTypeBuilder<ExamQuestion> builder)
    {
        builder.ToTable("ExamQuestions");

        builder.HasKey(q => q.Id);

        // Khớp cột điểm của bản đề và của lượt làm bài (numeric(6,2)): tổng điểm không được tràn cột
        // ExamAttempt.MaxScore ngay khi học sinh đầu tiên bắt đầu làm.
        builder.Property(q => q.Point)
            .HasPrecision(6, 2);

        // Xoá đề là xoá danh sách của nó.
        builder.HasOne(q => q.Exam)
            .WithMany(e => e.Questions)
            .HasForeignKey(q => q.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: câu hỏi đang nằm trong một đề thì không xoá được. QuestionService báo 409 trước khi tới
        // đây, nên khoá ngoại chỉ là lớp chặn cuối.
        builder.HasOne(q => q.Question)
            .WithMany(x => x.ExamQuestions)
            .HasForeignKey(q => q.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Một câu chỉ xuất hiện một lần trong một đề; thứ tự là thứ tự duy nhất trong đề.
        builder.HasIndex(q => new { q.ExamId, q.QuestionId })
            .IsUnique()
            .HasDatabaseName("IX_ExamQuestions_Exam_Question");

        builder.HasIndex(q => new { q.ExamId, q.Ordinal })
            .HasDatabaseName("IX_ExamQuestions_Exam_Ordinal");

        builder.HasIndex(q => q.QuestionId)
            .HasDatabaseName("IX_ExamQuestions_QuestionId");
    }
}
