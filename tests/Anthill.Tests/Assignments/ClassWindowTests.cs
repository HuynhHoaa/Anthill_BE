using Anthill.Application.Common;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Assignments;

/// <summary>
/// Khung giờ theo lớp khi giao nhiều lớp một lần: dời giờ về lịch đề, em mới vào lớp nhận đúng giờ của lớp,
/// và phép so "giờ có thật sự đổi không" dùng để quyết định có báo cho em hay không.
/// </summary>
public class ClassWindowTests
{
    private static readonly DateTime ExamOpen = new(2026, 9, 10, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExamClose = new(2026, 9, 17, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime TuesdayOpen = new(2026, 9, 15, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime TuesdayClose = new(2026, 9, 15, 2, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime ThursdayOpen = new(2026, 9, 17, 7, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ThursdayClose = new(2026, 9, 17, 8, 30, 0, DateTimeKind.Utc);

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

    private static ExamAssignment Row(DateTime? openAt, DateTime? closeAt, bool started = false) => new()
    {
        ExamId = 1,
        StudentId = 3,
        OpenAt = openAt,
        CloseAt = closeAt,
        Attempt = started ? new ExamAttempt { Status = AssignmentStatus.InProgress } : null
    };

    private static AssignmentSchedule.WindowSample Sample(
        DateTime? openAt,
        DateTime? closeAt,
        bool started = false,
        int count = 1,
        int assignedDay = 1) =>
        new(openAt, closeAt, started, count, new DateTime(2026, 9, assignedDay, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Doi_ve_lich_de_thi_go_gio_rieng_cua_em_chua_bat_dau()
    {
        var fresh = Row(TuesdayOpen, TuesdayClose);
        var running = Row(TuesdayOpen, TuesdayClose, started: true);

        var outcome = AssignmentSchedule.Reschedule([fresh, running], null, null);

        outcome.Updated.Should().Be(1);
        outcome.KeptStarted.Should().Be(1);
        AssignmentSchedule.HasOverride(fresh).Should().BeFalse();
        (running.OpenAt, running.CloseAt).Should().Be(((DateTime?)TuesdayOpen, (DateTime?)TuesdayClose));
        outcome.Changes.Should().ContainSingle()
            .Which.Should().Be(new AssignmentSchedule.RescheduleChange(fresh, TuesdayOpen, TuesdayClose));
    }

    [Fact]
    public void Gui_lai_dung_khung_cu_thi_khong_bao_em_da_bat_dau_giu_khung_cu()
    {
        // Em đã bắt đầu đang giữ đúng khung được gửi lên: không có gì khác biệt để báo cả.
        var running = Row(TuesdayOpen, TuesdayClose, started: true);

        var outcome = AssignmentSchedule.Reschedule([running], TuesdayOpen, TuesdayClose);

        outcome.KeptStarted.Should().Be(0);
        outcome.Updated.Should().Be(0);
        outcome.Changes.Should().BeEmpty();
    }

    [Fact]
    public void Dat_gio_rieng_trung_khit_lich_de_khong_tinh_la_doi_gio()
    {
        var exam = BuildExam();
        var row = Row(null, null);
        row.OpenAt = ExamOpen;
        row.CloseAt = ExamClose;

        AssignmentSchedule.EffectiveWindowChanged(row, exam, previousOpenAt: null, previousCloseAt: null)
            .Should().BeFalse();
    }

    [Fact]
    public void Doi_khung_that_su_thi_tinh_la_doi_gio()
    {
        var exam = BuildExam();
        var row = Row(ThursdayOpen, ThursdayClose);

        AssignmentSchedule.EffectiveWindowChanged(row, exam, TuesdayOpen, TuesdayClose).Should().BeTrue();
        AssignmentSchedule.EffectiveWindowChanged(row, exam, null, null).Should().BeTrue();
    }

    [Fact]
    public void Go_gio_rieng_ve_lich_de_la_doi_gio_neu_hai_khung_khac_nhau()
    {
        var exam = BuildExam();
        var row = Row(null, null);

        AssignmentSchedule.EffectiveWindowChanged(row, exam, TuesdayOpen, TuesdayClose).Should().BeTrue();
    }

    [Fact]
    public void Khung_dai_dien_lay_tu_cac_em_chua_bat_dau()
    {
        // Ba em đã vào thi theo khung thứ Ba; hai em còn lại vừa được dời sang thứ Năm. Em mới vào lớp phải
        // làm thứ Năm cùng hai bạn — số đông đã bắt đầu không được kéo em về khung cũ.
        var window = AssignmentSchedule.RepresentativeWindow(
        [
            Sample(TuesdayOpen, TuesdayClose, started: true, count: 3),
            Sample(ThursdayOpen, ThursdayClose, count: 2)
        ]);

        window.Should().Be(((DateTime?)ThursdayOpen, (DateTime?)ThursdayClose));
    }

    [Fact]
    public void Ca_lop_da_bat_dau_thi_xet_tren_toan_lop()
    {
        var window = AssignmentSchedule.RepresentativeWindow(
        [
            Sample(TuesdayOpen, TuesdayClose, started: true, count: 3),
            Sample(ThursdayOpen, ThursdayClose, started: true, count: 1)
        ]);

        window.Should().Be(((DateTime?)TuesdayOpen, (DateTime?)TuesdayClose));
    }

    [Fact]
    public void Hoa_thi_lay_khung_cua_lan_giao_gan_nhat()
    {
        var window = AssignmentSchedule.RepresentativeWindow(
        [
            Sample(TuesdayOpen, TuesdayClose, count: 2, assignedDay: 1),
            Sample(null, null, count: 2, assignedDay: 5)
        ]);

        window.Should().Be(((DateTime?)null, (DateTime?)null));
    }

    [Fact]
    public void Khong_co_mau_nao_thi_khong_co_khung_dai_dien()
    {
        AssignmentSchedule.RepresentativeWindow([]).Should().BeNull();
    }

    [Fact]
    public void Nua_cap_gio_duoc_gom_chung_voi_lich_de()
    {
        var window = AssignmentSchedule.RepresentativeWindow(
        [
            Sample(TuesdayOpen, null, count: 1),
            Sample(null, null, count: 1),
            Sample(ThursdayOpen, ThursdayClose, count: 1)
        ]);

        window.Should().Be(((DateTime?)null, (DateTime?)null));
    }

    private static readonly Dictionary<int, (DateTime? OpenAt, DateTime? CloseAt)> NoClassWindows = new();
    private static readonly Dictionary<int, (DateTime? OpenAt, DateTime? CloseAt)?> NoInherited = new();

    [Fact]
    public void Em_moi_cua_lop_khai_rieng_nhan_khung_cua_lop_do()
    {
        var classWindows = new Dictionary<int, (DateTime? OpenAt, DateTime? CloseAt)>
        {
            [7] = (TuesdayOpen, TuesdayClose),
            [8] = (null, null)
        };

        AssignmentSchedule.WindowForNewAssignment(7, (ThursdayOpen, ThursdayClose), classWindows, NoInherited)
            .Should().Be(((DateTime?)TuesdayOpen, (DateTime?)TuesdayClose));

        // Lớp khai "theo lịch đề" thì không lấy cặp giờ chung của request.
        AssignmentSchedule.WindowForNewAssignment(8, (ThursdayOpen, ThursdayClose), classWindows, NoInherited)
            .Should().Be(((DateTime?)null, (DateTime?)null));
    }

    [Fact]
    public void Em_moi_vao_lop_da_co_gio_rieng_thi_lam_cung_gio_voi_lop()
    {
        var inherited = new Dictionary<int, (DateTime? OpenAt, DateTime? CloseAt)?>
        {
            [7] = (TuesdayOpen, TuesdayClose)
        };

        AssignmentSchedule.WindowForNewAssignment(7, (null, null), NoClassWindows, inherited)
            .Should().Be(((DateTime?)TuesdayOpen, (DateTime?)TuesdayClose));
    }

    [Fact]
    public void Cap_gio_chung_cua_request_thang_khung_ke_thua()
    {
        var inherited = new Dictionary<int, (DateTime? OpenAt, DateTime? CloseAt)?>
        {
            [7] = (TuesdayOpen, TuesdayClose)
        };

        AssignmentSchedule.WindowForNewAssignment(7, (ThursdayOpen, ThursdayClose), NoClassWindows, inherited)
            .Should().Be(((DateTime?)ThursdayOpen, (DateTime?)ThursdayClose));
    }

    [Fact]
    public void Em_chon_dich_danh_chi_theo_request()
    {
        var inherited = new Dictionary<int, (DateTime? OpenAt, DateTime? CloseAt)?>
        {
            [7] = (TuesdayOpen, TuesdayClose)
        };

        AssignmentSchedule.WindowForNewAssignment(null, (null, null), NoClassWindows, inherited)
            .Should().Be(((DateTime?)null, (DateTime?)null));
        AssignmentSchedule.WindowForNewAssignment(null, (ThursdayOpen, ThursdayClose), NoClassWindows, inherited)
            .Should().Be(((DateTime?)ThursdayOpen, (DateTime?)ThursdayClose));
    }

    [Fact]
    public void Lop_moi_giao_lan_dau_khong_kem_gio_thi_theo_lich_de()
    {
        AssignmentSchedule.WindowForNewAssignment(9, (null, null), NoClassWindows, NoInherited)
            .Should().Be(((DateTime?)null, (DateTime?)null));
    }
}
