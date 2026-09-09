using EduQuiz.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduQuiz.Infrastructure.Persistence.Configurations;

/// <summary>
/// Bản đề đã sinh. <c>AssignmentId = NULL</c> nghĩa là đề chung cho cả lớp
/// (<c>Exam.RandomizePerStudent = false</c>).
/// </summary>
public class ExamPaperConfiguration : IEntityTypeConfiguration<ExamPaper>
{
    public void Configure(EntityTypeBuilder<ExamPaper> builder)
    {
        builder.ToTable("ExamPapers");

        builder.HasKey(p => p.Id);

        builder.HasOne(p => p.Exam)
            .WithMany(e => e.Papers)
            .HasForeignKey(p => p.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Thu hồi bài đã giao thì bản đề riêng của em đó cũng không còn lý do tồn tại.
        builder.HasOne(p => p.Assignment)
            .WithOne(a => a.Paper)
            .HasForeignKey<ExamPaper>(p => p.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.AssignmentId)
            .IsUnique()
            .HasDatabaseName("IX_ExamPapers_AssignmentId");

        // Chỉ mục lọc bên dưới không phục vụ được truy vấn "mọi bản đề của kỳ thi này" (xoá đề),
        // nên vẫn cần một chỉ mục thường trên khoá ngoại.
        builder.HasIndex(p => p.ExamId)
            .HasDatabaseName("IX_ExamPapers_ExamId");

        // Mỗi đề chỉ được có đúng một bản đề chung. Hai học sinh bấm "bắt đầu" cùng lúc sẽ
        // cùng muốn sinh đề chung — chỉ mục lọc này là thứ duy nhất chặn được bản sinh thứ hai.
        builder.HasIndex(p => p.ExamId, "IX_ExamPapers_SharedPaperPerExam")
            .IsUnique()
            .HasFilter("\"AssignmentId\" IS NULL");
    }
}
