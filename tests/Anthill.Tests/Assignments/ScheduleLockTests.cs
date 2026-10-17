using Anthill.Application.Common;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using FluentAssertions;

namespace Anthill.Tests.Assignments;

public class ScheduleLockTests
{
    private static readonly DateTime Open = new(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Close = new(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc);

    private static Exam BuildExam(bool isDraft = false) => new()
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
        OpenAt = Open,
        CloseAt = Close,
        IsDraft = isDraft,
        CreatedByUserId = 2
    };

    [Fact]
    public void Con_dung_120_phut_thi_van_doi_duoc()
    {
        AssignmentSchedule.IsScheduleLocked(Open, Close, Open.AddMinutes(-120)).Should().BeFalse();
    }

    [Fact]
    public void Con_119_phut_59_giay_thi_bi_khoa()
    {
        AssignmentSchedule.IsScheduleLocked(Open, Close, Open.AddMinutes(-120).AddSeconds(1)).Should().BeTrue();
    }

    [Fact]
    public void Con_xa_hon_120_phut_thi_doi_duoc()
    {
        AssignmentSchedule.IsScheduleLocked(Open, Close, Open.AddDays(-3)).Should().BeFalse();
    }

    [Fact]
    public void Dung_gio_mo_va_giua_gio_thi_bi_khoa()
    {
        AssignmentSchedule.IsScheduleLocked(Open, Close, Open).Should().BeTrue();
        AssignmentSchedule.IsScheduleLocked(Open, Close, Open.AddMinutes(30)).Should().BeTrue();
    }

    /// <summary>Bằng giờ đóng vẫn còn mở, nên vẫn khoá — cùng phép so với vòng đời của đề.</summary>
    [Fact]
    public void Dung_giay_dong_van_khoa_qua_giay_dong_thi_mo_khoa()
    {
        AssignmentSchedule.IsScheduleLocked(Open, Close, Close).Should().BeTrue();
        AssignmentSchedule.IsScheduleLocked(Open, Close, Close.AddSeconds(1)).Should().BeFalse();
    }

    /// <summary>Cùng một khoảnh khắc ghi dưới giờ máy (Kind = Local) vẫn phải cho cùng kết quả.</summary>
    [Fact]
    public void Gio_ghi_duoi_gio_may_van_so_theo_cung_mot_khoanh_khac()
    {
        var openLocal = Open.ToLocalTime();
        var closeLocal = Close.ToLocalTime();

        AssignmentSchedule.IsScheduleLocked(openLocal, closeLocal, Open.AddMinutes(-60).ToLocalTime())
            .Should().BeTrue();
        AssignmentSchedule.IsScheduleLocked(openLocal, closeLocal, Open.AddMinutes(-120).ToLocalTime())
            .Should().BeFalse();
        AssignmentSchedule.IsScheduleLocked(openLocal, closeLocal, Close.AddSeconds(1).ToLocalTime())
            .Should().BeFalse();
    }

    /// <summary>
    /// Đề đã giao mà chưa ai làm vẫn đưa về nháp được, nên nháp không được miễn khoá: không thì đưa về nháp,
    /// dời lịch, xuất bản lại là lách được.
    /// </summary>
    [Fact]
    public void De_nhap_ma_con_luot_giao_van_khoa()
    {
        AssignmentSchedule.IsExamScheduleLocked(BuildExam(isDraft: true), assignedCount: 5, Open.AddMinutes(10))
            .Should().BeTrue();
    }

    [Fact]
    public void De_nhap_chua_tung_giao_thi_khong_khoa()
    {
        AssignmentSchedule.IsExamScheduleLocked(BuildExam(isDraft: true), assignedCount: 0, Open.AddMinutes(10))
            .Should().BeFalse();
    }

    [Fact]
    public void De_chua_giao_cho_ai_thi_khong_khoa_du_da_toi_gio()
    {
        AssignmentSchedule.IsExamScheduleLocked(BuildExam(), assignedCount: 0, Open.AddMinutes(10))
            .Should().BeFalse();
    }

    [Fact]
    public void De_da_giao_va_sap_mo_thi_khoa()
    {
        AssignmentSchedule.IsExamScheduleLocked(BuildExam(), assignedCount: 1, Open.AddMinutes(-90))
            .Should().BeTrue();
    }

    [Fact]
    public void De_da_giao_va_da_dong_thi_mo_khoa_lai()
    {
        AssignmentSchedule.IsExamScheduleLocked(BuildExam(), assignedCount: 1, Close.AddMinutes(1))
            .Should().BeFalse();
    }

    [Fact]
    public void Gui_lai_dung_lich_cu_khong_phai_la_doi_lich_de()
    {
        var exam = BuildExam();

        AssignmentSchedule.ExamWindowChanges(exam, Open, Close).Should().BeFalse();
        AssignmentSchedule.ExamWindowChanges(exam, DateTime.SpecifyKind(Open, DateTimeKind.Unspecified), Close)
            .Should().BeFalse();
        AssignmentSchedule.ExamWindowChanges(exam, Open.AddMinutes(1), Close).Should().BeTrue();
        AssignmentSchedule.ExamWindowChanges(exam, Open, Close.AddMinutes(-1)).Should().BeTrue();
    }

    [Fact]
    public void Gui_lai_dung_cap_gio_rieng_khong_phai_la_doi_gio()
    {
        var assignment = new ExamAssignment { OpenAt = Open, CloseAt = Close };

        AssignmentSchedule.WindowWouldChange(assignment, Open, Close).Should().BeFalse();
        AssignmentSchedule.WindowWouldChange(assignment, Open.AddHours(1), Close.AddHours(1)).Should().BeTrue();
    }

    [Fact]
    public void Cung_cap_gio_ghi_duoi_gio_may_khong_phai_la_doi_gio()
    {
        var assignment = new ExamAssignment { OpenAt = Open, CloseAt = Close };

        AssignmentSchedule.WindowWouldChange(assignment, Open.ToLocalTime(), Close.ToLocalTime()).Should().BeFalse();
    }

    [Fact]
    public void Ban_ghi_theo_lich_de_gui_cap_rong_thi_khong_doi_con_gui_gio_thi_doi()
    {
        var assignment = new ExamAssignment { OpenAt = null, CloseAt = null };

        AssignmentSchedule.WindowWouldChange(assignment, null, null).Should().BeFalse();
        AssignmentSchedule.WindowWouldChange(assignment, Open, Close).Should().BeTrue();
    }

    /// <summary>Thiếu một nửa thì cả cặp bị coi là NULL, như <c>Reschedule</c> vẫn làm.</summary>
    [Fact]
    public void Thieu_mot_nua_cap_gio_duoc_coi_la_ve_lich_de()
    {
        var own = new ExamAssignment { OpenAt = Open, CloseAt = Close };
        var followsExam = new ExamAssignment { OpenAt = null, CloseAt = null };

        AssignmentSchedule.WindowWouldChange(own, Open, null).Should().BeTrue();
        AssignmentSchedule.WindowWouldChange(followsExam, Open, null).Should().BeFalse();
    }

    [Fact]
    public void Cau_bao_noi_dung_luc_sap_mo_va_luc_dang_mo()
    {
        var before = ScheduleLockMessage.For(ScheduleLockMessage.ExamSubject, Open, Open.AddMinutes(-30));
        var during = ScheduleLockMessage.For(ScheduleLockMessage.ClassSubject("12A1"), Open, Open.AddMinutes(5));

        before.Should().Be("Lịch chung của đề còn chưa đầy 2 giờ nữa là mở nên không đổi giờ được.");
        during.Should().Be("Bài của lớp 12A1 đang mở nên không đổi giờ được.");
    }

    [Fact]
    public void Ten_lop_da_co_chu_Lop_thi_khong_lap_lai()
    {
        ScheduleLockMessage.ClassSubject("Lớp 10A2").Should().Be("Bài của Lớp 10A2");
        ScheduleLockMessage.ClassSubject("12A1").Should().Be("Bài của lớp 12A1");
    }

    [Fact]
    public void Co_khoa_tren_dong_giao_bai_theo_gio_that_cua_em()
    {
        var exam = BuildExam();
        var followsExam = new ExamAssignment { ExamId = 1, StudentId = 3, Exam = exam };
        var ownWindow = new ExamAssignment
        {
            ExamId = 1,
            StudentId = 4,
            Exam = exam,
            OpenAt = Open.AddDays(5),
            CloseAt = Open.AddDays(5).AddHours(2)
        };
        var now = Open.AddMinutes(-30);

        followsExam.ToDto(now).ScheduleLocked.Should().BeTrue();
        ownWindow.ToDto(now).ScheduleLocked.Should().BeFalse();
    }

    [Fact]
    public void Co_khoa_tren_de_theo_so_luot_giao_va_gio_mo_cua_de()
    {
        var exam = BuildExam();
        var now = Open.AddMinutes(-30);

        exam.ToDto(now, assignedCount: 0, submittedCount: 0, startedCount: 0).ScheduleLocked.Should().BeFalse();
        exam.ToDto(now, assignedCount: 3, submittedCount: 0, startedCount: 0).ScheduleLocked.Should().BeTrue();
        exam.ToDto(Close.AddMinutes(1), assignedCount: 3, submittedCount: 3, startedCount: 3)
            .ScheduleLocked.Should().BeFalse();
    }

    [Fact]
    public void Co_khoa_tren_tung_khung_gio_cua_lop()
    {
        var exam = BuildExam();
        var assigned = Open.AddDays(-1);
        var farWindow = new AssignmentWindowRow(1, 1, "12A1", Open.AddDays(5), Open.AddDays(5).AddHours(2), false, 20, assigned);
        var nearWindow = new AssignmentWindowRow(1, 2, "12A2", Open.AddMinutes(20), Open.AddHours(2), false, 20, assigned);

        var windows = AssignmentWindowSummary.Build(exam, [farWindow, nearWindow], asOfUtc: Open);

        windows.Single(window => window.GroupName == "12A1").ScheduleLocked.Should().BeFalse();
        windows.Single(window => window.GroupName == "12A2").ScheduleLocked.Should().BeTrue();
    }
}
