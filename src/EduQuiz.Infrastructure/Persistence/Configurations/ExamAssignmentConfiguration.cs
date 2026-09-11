using EduQuiz.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduQuiz.Infrastructure.Persistence.Configurations;

public class ExamAssignmentConfiguration : IEntityTypeConfiguration<ExamAssignment>
{
    public void Configure(EntityTypeBuilder<ExamAssignment> builder)
    {
        // Validator của request giao bài chỉ canh đúng một cửa vào. Hai ràng buộc này canh mọi cửa còn
        // lại — một đường ghi mới về sau, hay một lần sửa tay dưới database: cặp giờ riêng có đủ hai
        // nửa hoặc không có gì, và giờ đóng luôn sau giờ mở.
        builder.ToTable("ExamAssignments", table =>
        {
            table.HasCheckConstraint(
                "CK_ExamAssignments_SchedulePair",
                "(\"OpenAt\" IS NULL) = (\"CloseAt\" IS NULL)");
            table.HasCheckConstraint(
                "CK_ExamAssignments_ScheduleOrder",
                "\"OpenAt\" IS NULL OR \"OpenAt\" < \"CloseAt\"");
        });

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

        // Xoá một lớp chỉ gỡ dấu "giao qua lớp nào", không xoá bài và không đụng khung giờ riêng đã đặt —
        // cùng tinh thần với User.GroupId. Restrict thì lớp đã từng được giao bài không bao giờ xoá được.
        builder.HasOne(a => a.AssignedViaGroup)
            .WithMany()
            .HasForeignKey(a => a.AssignedViaGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        // Gửi lại một lớp để sửa khung giờ tra theo cột này, và khoá ngoại SET NULL cũng quét theo nó
        // mỗi lần xoá lớp.
        builder.HasIndex(a => a.AssignedViaGroupId)
            .HasDatabaseName("IX_ExamAssignments_AssignedViaGroupId");

        // Đây chính là thứ làm cho việc giao bài idempotent (§5): giao lại cho học sinh đã có
        // bài thì bỏ qua, không tạo bản ghi thứ hai — kể cả khi hai request tới cùng lúc.
        builder.HasIndex(a => new { a.ExamId, a.StudentId })
            .IsUnique()
            .HasDatabaseName("IX_ExamAssignments_Exam_Student");

        builder.HasIndex(a => a.StudentId)
            .HasDatabaseName("IX_ExamAssignments_StudentId");
    }
}
