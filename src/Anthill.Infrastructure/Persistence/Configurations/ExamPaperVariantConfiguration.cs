using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

/// <summary>Các đề nhỏ của một lớp trên một đề được chia thành nhiều đề (<c>Exam.PaperCount &gt; 1</c>).</summary>
public class ExamPaperVariantConfiguration : IEntityTypeConfiguration<ExamPaperVariant>
{
    public void Configure(EntityTypeBuilder<ExamPaperVariant> builder)
    {
        builder.ToTable("ExamPaperVariants");

        builder.HasKey(v => v.Id);

        // Mảng số nguyên của PostgreSQL: danh sách mã câu hỏi nhỏ (vài chục phần tử) và luôn đọc trọn cả mảng.
        builder.Property(v => v.QuestionIds)
            .HasColumnType("integer[]");

        // Xoá đề hay xoá lớp là xoá cách chia của nó.
        builder.HasOne(v => v.Exam)
            .WithMany()
            .HasForeignKey(v => v.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.Group)
            .WithMany()
            .HasForeignKey(v => v.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Một lớp chỉ có một bộ đề nhỏ trên một đề. Hai lần giao đồng thời vào cùng một lớp xếp hàng nhau ở khoá dòng đề;
        // chỉ mục này là lớp chặn cuối nếu có gì đó lọt qua.
        builder.HasIndex(v => new { v.ExamId, v.GroupId, v.VariantIndex })
            .IsUnique()
            .HasDatabaseName("IX_ExamPaperVariants_Exam_Group_Variant");
    }
}
