using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Results;

/// <summary>
/// Diem mo ngay khi cham, dap an tung cau cho toi khi MOI luot giao cua de da qua gio dong. Em lop
/// sang lam xong som khong duoc cam dap an de chuyen cho lop chieu chua thi.
/// </summary>
public class AnswerVisibilityTests
{
    private static readonly DateTime ReleaseAt = new(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc);

    private static ExamAttempt Attempt(bool showResultAfterSubmit, AssignmentStatus status)
    {
        var exam = new Exam
        {
            Id = 1,
            Title = "De chung",
            DurationMinutes = 45,
            TotalQuestions = 9,
            OpenAt = new DateTime(2026, 9, 10, 1, 0, 0, DateTimeKind.Utc),
            CloseAt = ReleaseAt,
            ShowResultAfterSubmit = showResultAfterSubmit,
            CreatedByUserId = 2
        };

        return new ExamAttempt
        {
            Id = 21,
            Status = status,
            Score = 7,
            MaxScore = 9,
            CorrectCount = 7,
            Assignment = new ExamAssignment { Id = 10, ExamId = 1, StudentId = 3, Exam = exam }
        };
    }

    [Fact]
    public void Giao_vien_so_huu_luon_thay_dap_an_ke_ca_truoc_gio_mo()
    {
        var result = Attempt(false, AssignmentStatus.Submitted)
            .ResolveAnswerVisibility(isOwningTeacher: true, ReleaseAt, ReleaseAt.AddDays(-2));

        result.CanSee.Should().BeTrue();
        result.VisibleAt.Should().BeNull();
    }

    [Fact]
    public void Hoc_sinh_thay_diem_ngay_nhung_dap_an_cho_lop_dong_muon_nhat()
    {
        var attempt = Attempt(true, AssignmentStatus.Graded);

        var result = attempt.ResolveAnswerVisibility(false, ReleaseAt, ReleaseAt.AddHours(-1));

        attempt.CanRevealScore(isOwningTeacher: false).Should().BeTrue();
        result.CanSee.Should().BeFalse();
        result.VisibleAt.Should().Be(ReleaseAt);
    }

    [Fact]
    public void Qua_moc_dong_muon_nhat_thi_dap_an_mo()
    {
        var result = Attempt(true, AssignmentStatus.Graded)
            .ResolveAnswerVisibility(false, ReleaseAt, ReleaseAt.AddSeconds(1));

        result.CanSee.Should().BeTrue();
        result.VisibleAt.Should().BeNull();
    }

    [Fact]
    public void Dung_giay_dong_bai_van_chua_mo()
    {
        var result = Attempt(true, AssignmentStatus.Graded)
            .ResolveAnswerVisibility(false, ReleaseAt, ReleaseAt);

        result.CanSee.Should().BeFalse();
        result.VisibleAt.Should().Be(ReleaseAt);
    }

    [Fact]
    public void Tat_showResultAfterSubmit_thi_khong_mo_va_khong_hen_gio()
    {
        var attempt = Attempt(false, AssignmentStatus.Graded);

        var result = attempt.ResolveAnswerVisibility(false, ReleaseAt, ReleaseAt.AddDays(30));

        result.CanSee.Should().BeFalse();
        result.VisibleAt.Should().BeNull();
        attempt.CanRevealScore(isOwningTeacher: false).Should().BeFalse();
    }

    [Theory]
    [InlineData(AssignmentStatus.InProgress)]
    [InlineData(AssignmentStatus.Submitted)]
    [InlineData(AssignmentStatus.Expired)]
    public void Bai_chua_cham_thi_khong_hen_gio(AssignmentStatus status)
    {
        var result = Attempt(true, status).ResolveAnswerVisibility(false, ReleaseAt, ReleaseAt.AddDays(1));

        result.CanSee.Should().BeFalse();
        result.VisibleAt.Should().BeNull();
    }

    [Fact]
    public void Moc_hen_luon_la_UTC()
    {
        var unspecified = DateTime.SpecifyKind(ReleaseAt, DateTimeKind.Unspecified);

        var result = Attempt(true, AssignmentStatus.Graded)
            .ResolveAnswerVisibility(false, unspecified, ReleaseAt.AddHours(-1));

        result.VisibleAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }
}
