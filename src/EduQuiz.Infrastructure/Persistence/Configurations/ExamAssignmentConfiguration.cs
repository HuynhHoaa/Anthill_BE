using EduQuiz.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduQuiz.Infrastructure.Persistence.Configurations;

public class ExamAssignmentConfiguration : IEntityTypeConfiguration<ExamAssignment>
{
    public void Configure(EntityTypeBuilder<ExamAssignment> builder)
    {
        builder.ToTable("ExamAssignments");

        builder.HasKey(a => a.Id);

        // Xoá đề kéo theo toàn bộ bản ghi giao bài (§4.6).
        builder.HasOne(a => a.Exam)
            .WithMany(e => e.Assignments)
            .HasForeignKey(a => a.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tài khoản học sinh chỉ bị khoá chứ không bị xoá (§4.3 không có endpoint xoá),
        // nên chặn hẳn để không ai vô tình cuốn theo cả lịch sử làm bài.
        builder.HasOne(a => a.Student)
            .WithMany(u => u.Assignments)
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Đây chính là thứ làm cho việc giao bài idempotent (§5): giao lại cho học sinh đã có
        // bài thì bỏ qua, không tạo bản ghi thứ hai — kể cả khi hai request tới cùng lúc.
        builder.HasIndex(a => new { a.ExamId, a.StudentId })
            .IsUnique()
            .HasDatabaseName("IX_ExamAssignments_Exam_Student");

        builder.HasIndex(a => a.StudentId)
            .HasDatabaseName("IX_ExamAssignments_StudentId");
    }
}
