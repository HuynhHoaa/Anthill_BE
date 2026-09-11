using Anthill.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anthill.Infrastructure.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Content)
            .IsRequired()
            .HasMaxLength(2000);

        // Ảnh nằm trên Cloudinary, database chỉ giữ URL.
        builder.Property(q => q.ImageUrl)
            .HasMaxLength(512);

        builder.Property(q => q.Topic)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(q => q.Difficulty)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(q => q.Status)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(q => q.Visibility)
            .IsRequired()
            .HasMaxLength(16)
            .HasConversion<string>();

        builder.Property(q => q.DefaultPoint)
            .HasPrecision(6, 2);

        builder.HasOne(q => q.Subject)
            .WithMany(s => s.Questions)
            .HasForeignKey(q => q.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // Xoá tài khoản không được kéo theo câu hỏi đã soạn: ngân hàng là tài sản của tổ bộ môn,
        // không phải của một người.
        builder.HasOne(q => q.CreatedBy)
            .WithMany(u => u.CreatedQuestions)
            .HasForeignKey(q => q.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Chỉ mục phủ đúng mệnh đề phạm vi của §6:
        // SubjectId = @subject AND (Visibility = 'Shared' OR CreatedByUserId = @me) AND Status = 'Active'.
        builder.HasIndex(q => new { q.SubjectId, q.Status, q.Visibility })
            .HasDatabaseName("IX_Questions_Subject_Status_Visibility");

        builder.HasIndex(q => new { q.SubjectId, q.Difficulty })
            .HasDatabaseName("IX_Questions_Subject_Difficulty");

        builder.HasIndex(q => new { q.SubjectId, q.Topic })
            .HasDatabaseName("IX_Questions_Subject_Topic");

        builder.HasIndex(q => q.CreatedByUserId)
            .HasDatabaseName("IX_Questions_CreatedByUserId");
    }
}
