using Anthill.Application.Common;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Assignments;

public class AssignmentScheduleTests
{
    private static readonly DateTime ExamOpen = new(2026, 9, 10, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExamClose = new(2026, 9, 17, 1, 0, 0, DateTimeKind.Utc);

    private static Exam BuildExam() => new()
    {
        Id = 1,
        Title = "De chung",
        DurationMinutes = 45,
        TotalQuestions = 9,
        EasyPercentage = 45,
        MediumPercentage = 33,
        HardPercentage = 22,
        PointEasy = 1,
        PointMedium = 1,
        PointHard = 1,
        OpenAt = ExamOpen,
        CloseAt = ExamClose,
        CreatedByUserId = 2
    };

    private static ExamAssignment BuildAssignment() => new()
    {
        Id = 10,
        ExamId = 1,
        StudentId = 3,
        Exam = BuildExam()
    };

    [Fact]
    public void Khong_dat_gio_rieng_thi_theo_lich_chung_cua_de()
    {
        var (openAt, closeAt) = AssignmentSchedule.Resolve(BuildAssignment());

        openAt.Should().Be(ExamOpen);
        closeAt.Should().Be(ExamClose);
    }

    [Fact]
    public void Dat_gio_rieng_thi_gio_rieng_thang()
    {
        var assignment = BuildAssignment();
        assignment.OpenAt = new DateTime(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc);
        assignment.CloseAt = new DateTime(2026, 9, 11, 3, 0, 0, DateTimeKind.Utc);

        var (openAt, closeAt) = AssignmentSchedule.Resolve(assignment);

        openAt.Should().Be(assignment.OpenAt!.Value);
        closeAt.Should().Be(assignment.CloseAt!.Value);
        openAt.Should().NotBe(ExamOpen);
    }

    /// <summary>
    /// Hai lop cung mot de, hai khung gio khac nhau — day la ca dat ra toan bo tinh nang nay.
    /// </summary>
    [Fact]
    public void Hai_lop_tren_cung_mot_de_co_hai_khung_gio_doc_lap()
    {
        var exam = BuildExam();

        var sang = new ExamAssignment
        {
            ExamId = 1, StudentId = 3, Exam = exam,
            OpenAt = new DateTime(2026, 9, 15, 1, 0, 0, DateTimeKind.Utc),
            CloseAt = new DateTime(2026, 9, 15, 2, 0, 0, DateTimeKind.Utc)
        };

        var chieu = new ExamAssignment
        {
            ExamId = 1, StudentId = 4, Exam = exam,
            OpenAt = new DateTime(2026, 9, 17, 7, 0, 0, DateTimeKind.Utc),
            CloseAt = new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc)
        };

        AssignmentSchedule.Resolve(sang).OpenAt
            .Should().NotBe(AssignmentSchedule.Resolve(chieu).OpenAt);
    }

    /// <summary>
    /// Dat mot nua la trang thai vo nghia: bo ca nua da dat, lay tron lich chung — khong ghep.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Chi_dat_mot_nua_thi_quay_ve_lich_chung(bool hasOpen, bool hasClose)
    {
        var assignment = BuildAssignment();
        if (hasOpen) assignment.OpenAt = new DateTime(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc);
        if (hasClose) assignment.CloseAt = new DateTime(2026, 9, 11, 3, 0, 0, DateTimeKind.Utc);

        var (openAt, closeAt) = AssignmentSchedule.Resolve(assignment);

        openAt.Should().Be(ExamOpen);
        closeAt.Should().Be(ExamClose);
        AssignmentSchedule.HasOverride(assignment).Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void HasOverride_va_Resolve_luon_noi_cung_mot_dieu(bool hasOpen, bool hasClose)
    {
        var assignment = BuildAssignment();
        if (hasOpen) assignment.OpenAt = new DateTime(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc);
        if (hasClose) assignment.CloseAt = new DateTime(2026, 9, 11, 3, 0, 0, DateTimeKind.Utc);

        AssignmentSchedule.HasOverride(assignment)
            .Should().Be(AssignmentSchedule.Resolve(assignment) != (ExamOpen, ExamClose));
    }

    /// <summary>Mot nua gio rieng khong phai gio rieng, nen van can Exam.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Chi_dat_mot_nua_ma_thieu_Exam_thi_van_nem_loi_ro_rang(bool hasOpen, bool hasClose)
    {
        var assignment = new ExamAssignment { ExamId = 1, StudentId = 3 };
        if (hasOpen) assignment.OpenAt = new DateTime(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc);
        if (hasClose) assignment.CloseAt = new DateTime(2026, 9, 11, 3, 0, 0, DateTimeKind.Utc);

        var act = () => AssignmentSchedule.Resolve(assignment);

        act.Should().Throw<InvalidOperationException>().WithMessage("*chưa được nạp*");
    }

    [Fact]
    public void Resolve_co_du_gio_rieng_thi_khong_can_Exam()
    {
        var open = new DateTime(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc);
        var close = new DateTime(2026, 9, 11, 3, 0, 0, DateTimeKind.Utc);
        var assignment = new ExamAssignment { ExamId = 1, StudentId = 3, OpenAt = open, CloseAt = close };

        AssignmentSchedule.Resolve(assignment).Should().Be((open, close));
    }

    /// <summary>
    /// Khac Resolve: co nhap chi nam tren de, nen vong doi luon can Exam. Truoc day nhanh nay no ra
    /// NullReferenceException — mot loi 500 khong chi ve dau ca.
    /// </summary>
    [Fact]
    public void Vong_doi_theo_ban_ghi_thieu_Exam_thi_nem_loi_ro_rang_ke_ca_khi_co_gio_rieng()
    {
        var assignment = new ExamAssignment
        {
            ExamId = 1,
            StudentId = 3,
            OpenAt = new DateTime(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc),
            CloseAt = new DateTime(2026, 9, 11, 3, 0, 0, DateTimeKind.Utc)
        };

        var act = () => assignment.ResolveLifecycle(ExamOpen);

        act.Should().Throw<InvalidOperationException>().WithMessage("*chưa được nạp*");
    }

    [Fact]
    public void Thieu_Exam_va_khong_co_gio_rieng_thi_vong_doi_cung_nem_loi_ro_rang()
    {
        var assignment = new ExamAssignment { ExamId = 1, StudentId = 3 };

        var act = () => assignment.ResolveLifecycle(ExamOpen);

        act.Should().Throw<InvalidOperationException>().WithMessage("*chưa được nạp*");
    }

    [Fact]
    public void Resolve_voi_de_tuong_minh_khong_can_navigation()
    {
        var withNavigation = BuildAssignment();
        var withoutNavigation = new ExamAssignment { Id = 10, ExamId = 1, StudentId = 3 };

        AssignmentSchedule.Resolve(withoutNavigation, BuildExam())
            .Should().Be(AssignmentSchedule.Resolve(withNavigation));
    }

    [Fact]
    public void De_dua_vao_khong_khop_ExamId_thi_nem_loi()
    {
        var assignment = new ExamAssignment { ExamId = 2, StudentId = 3 };

        var act = () => AssignmentSchedule.Resolve(assignment, BuildExam());

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(20, 45, false)]
    [InlineData(44, 45, false)]
    [InlineData(45, 45, true)]
    [InlineData(46, 45, true)]
    [InlineData(120, 45, true)]
    public void Khung_gio_rieng_phai_chua_tron_thoi_luong(int windowMinutes, int duration, bool expected)
    {
        AssignmentSchedule.FitsDuration(ExamOpen, ExamOpen.AddMinutes(windowMinutes), duration)
            .Should().Be(expected);
    }

    [Fact]
    public void Khung_gio_nguoc_thi_khong_vua()
    {
        AssignmentSchedule.FitsDuration(ExamOpen, ExamOpen.AddMinutes(-60), 45).Should().BeFalse();
    }

    [Fact]
    public void Kind_khac_nhau_van_tinh_dung_khoang()
    {
        var closeLocal = ExamOpen.AddMinutes(60).ToLocalTime();

        AssignmentSchedule.FitsDuration(ExamOpen, closeLocal, 45).Should().BeTrue();
    }

    private static ExamAssignment Row(DateTime? openAt, DateTime? closeAt, AssignmentStatus? attempt) => new()
    {
        ExamId = 1,
        StudentId = 3,
        OpenAt = openAt,
        CloseAt = closeAt,
        Attempt = attempt is { } status ? new ExamAttempt { Status = status } : null
    };

    [Fact]
    public void Doi_khung_gio_chi_ap_cho_ban_ghi_chua_bat_dau()
    {
        var oldOpen = new DateTime(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc);
        var oldClose = new DateTime(2026, 9, 11, 3, 0, 0, DateTimeKind.Utc);
        var t1 = new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 9, 12, 4, 0, 0, DateTimeKind.Utc);

        var fresh = Row(null, null, null);
        var running = Row(oldOpen, oldClose, AssignmentStatus.InProgress);
        var graded = Row(null, null, AssignmentStatus.Graded);

        var outcome = AssignmentSchedule.Reschedule([fresh, running, graded], t1, t2);

        outcome.Updated.Should().Be(1);
        outcome.KeptStarted.Should().Be(2);
        (fresh.OpenAt, fresh.CloseAt).Should().Be((t1, t2));
        (running.OpenAt, running.CloseAt).Should().Be((oldOpen, oldClose));
        (graded.OpenAt, graded.CloseAt).Should().Be(((DateTime?)null, (DateTime?)null));
        AssignmentSchedule.HasOverride(fresh).Should().BeTrue();
    }

    [Fact]
    public void Khung_gio_da_dung_thi_khong_dem()
    {
        var t1 = new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 9, 12, 4, 0, 0, DateTimeKind.Utc);
        var row = Row(t1, t2, null);

        AssignmentSchedule.Reschedule([row], t1, t2).Updated.Should().Be(0);
        (row.OpenAt, row.CloseAt).Should().Be((t1, t2));
    }

    [Fact]
    public void Doi_khung_gio_chuan_hoa_ve_UTC()
    {
        var t1 = new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 9, 12, 4, 0, 0, DateTimeKind.Utc);
        var row = Row(t1, t2, null);

        // Cung khoanh khac, khac Kind: khong phai doi gio.
        AssignmentSchedule.Reschedule(
                [row],
                DateTime.SpecifyKind(t1, DateTimeKind.Unspecified),
                DateTime.SpecifyKind(t2, DateTimeKind.Unspecified))
            .Updated.Should().Be(0);

        // Khoanh khac khac, gui duoi Kind Local: luu lai la UTC, dung khoanh khac.
        var newOpen = t1.AddHours(5).ToLocalTime();
        var newClose = t2.AddHours(5).ToLocalTime();
        AssignmentSchedule.Reschedule([row], newOpen, newClose).Updated.Should().Be(1);
        row.OpenAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        row.OpenAt.Value.Should().Be(t1.AddHours(5));
    }

    [Fact]
    public void HasOverride_chi_dung_khi_co_du_ca_hai_moc()
    {
        var assignment = BuildAssignment();
        AssignmentSchedule.HasOverride(assignment).Should().BeFalse();

        assignment.OpenAt = ExamOpen;
        assignment.CloseAt = ExamClose;
        AssignmentSchedule.HasOverride(assignment).Should().BeTrue();
    }

    /// <summary>
    /// Im lang tra ve default(DateTime) se thanh nam 0001 — moi bai deu "da dong tu lau", va loi
    /// do se bi doc nham thanh loi nghiep vu thay vi loi nap du lieu.
    /// </summary>
    [Fact]
    public void Thieu_Exam_ma_khong_co_gio_rieng_thi_nem_loi_ro_rang()
    {
        var assignment = new ExamAssignment { ExamId = 1, StudentId = 3 };

        var act = () => AssignmentSchedule.Resolve(assignment);

        act.Should().Throw<InvalidOperationException>().WithMessage("*chưa được nạp*");
    }
}
