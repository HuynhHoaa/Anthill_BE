using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

public class AcademicTermConfiguration : IEntityTypeConfiguration<AcademicTerm>
{
    public void Configure(EntityTypeBuilder<AcademicTerm> builder)
    {
        builder.ToTable("AcademicTerms");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.IsCurrent)
            .HasDefaultValue(false);

        // Tên kỳ không trùng nhau (dịch vụ còn so không phân biệt hoa thường và dấu trước khi lưu).
        builder.HasIndex(t => t.Name)
            .IsUnique()
            .HasDatabaseName("IX_AcademicTerms_Name");

        // Tối đa MỘT kỳ hiện tại: chỉ mục duy nhất chỉ tính các dòng IsCurrent = true.
        builder.HasIndex(t => t.IsCurrent)
            .IsUnique()
            .HasFilter("\"IsCurrent\" = TRUE")
            .HasDatabaseName("IX_AcademicTerms_IsCurrent");

        // Xoá kỳ còn lớp bị chặn ở dịch vụ (409); khoá ngoại Restrict là lưới đỡ.
        builder.HasMany(t => t.Groups)
            .WithOne(g => g.AcademicTerm)
            .HasForeignKey(g => g.AcademicTermId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
