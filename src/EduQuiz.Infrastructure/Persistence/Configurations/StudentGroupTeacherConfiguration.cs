using EduQuiz.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduQuiz.Infrastructure.Persistence.Configurations;

public class StudentGroupTeacherConfiguration : IEntityTypeConfiguration<StudentGroupTeacher>
{
    public void Configure(EntityTypeBuilder<StudentGroupTeacher> builder)
    {
        builder.ToTable("StudentGroupTeachers");

        // Khoá kép thay cho một cột Id riêng: nó khiến "phân công cùng một giáo viên hai lần vào
        // cùng một lớp" trở thành điều database từ chối, thay vì một quy tắc tầng ứng dụng phải
        // nhớ kiểm trước mỗi lần ghi.
        builder.HasKey(t => new { t.StudentGroupId, t.TeacherId });

        // Xoá lớp thì gỡ luôn các dòng phân công của nó — bản ghi phân công không có ý nghĩa gì
        // khi lớp đã biến mất.
        builder.HasOne(t => t.StudentGroup)
            .WithMany(g => g.Teachers)
            .HasForeignKey(t => t.StudentGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Nhưng xoá giáo viên thì KHÔNG được tự động gỡ: tài khoản giáo viên vốn bị khoá chứ không
        // xoá (§4.2), và nếu có xoá thật thì việc lớp mất người phụ trách phải nổi lên thành lỗi
        // để ai đó phân công lại, chứ không lặng lẽ biến mất.
        builder.HasOne(t => t.Teacher)
            .WithMany(u => u.TeachingGroups)
            .HasForeignKey(t => t.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.TeacherId)
            .HasDatabaseName("IX_StudentGroupTeachers_TeacherId");
    }
}
