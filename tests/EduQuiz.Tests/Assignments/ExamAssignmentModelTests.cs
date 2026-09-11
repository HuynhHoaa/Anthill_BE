using EduQuiz.Domain.Entities;
using EduQuiz.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EduQuiz.Tests.Assignments;

/// <summary>
/// Kiem model EF ngoai tuyen — doc Model khong mo ket noi nao. Cot AssignedViaGroupId tung ra doi
/// khong co khoa ngoai, nen xoa lop de lai id tro vao hu khong.
/// </summary>
public class ExamAssignmentModelTests
{
    private static AppDbContext Context() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=model-only").Options);

    [Fact]
    public void Lop_giao_bai_co_khoa_ngoai_SET_NULL_va_chi_muc()
    {
        using var context = Context();
        var entity = context.Model.FindEntityType(typeof(ExamAssignment))!;

        var fk = entity.GetForeignKeys()
            .Single(key => key.Properties.Single().Name == nameof(ExamAssignment.AssignedViaGroupId));

        fk.PrincipalEntityType.ClrType.Should().Be(typeof(StudentGroup));
        fk.DeleteBehavior.Should().Be(DeleteBehavior.SetNull);
        fk.IsRequired.Should().BeFalse();

        entity.GetIndexes().Should().Contain(index =>
            index.Properties.Count == 1 && index.Properties[0].Name == nameof(ExamAssignment.AssignedViaGroupId));
    }

    /// <summary>
    /// Rang buoc CHECK chi song trong model luc thiet ke (model chay that da luoc bo), nen phai doc qua
    /// IDesignTimeModel — chinh model ma migration dung.
    /// </summary>
    [Fact]
    public void Cap_gio_rieng_duoc_rang_buoc_o_database()
    {
        using var context = Context();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ExamAssignment))!;

        entity.GetCheckConstraints().Select(check => check.Name)
            .Should().Contain(["CK_ExamAssignments_SchedulePair", "CK_ExamAssignments_ScheduleOrder"]);
    }
}
