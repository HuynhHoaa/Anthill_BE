using Anthill.Application.Common;
using Anthill.Domain.Entities;
using FluentAssertions;

namespace Anthill.Tests.Exams;

/// <summary>
/// Khung giờ theo lớp trên màn hình giáo viên: mỗi lớp một dòng với giờ thật đang áp, học sinh chọn lẻ gom
/// theo giờ, và trạng thái mở/đóng tính cùng một phép so với phía học sinh.
/// </summary>
public class AssignmentWindowSummaryTests
{
    private static readonly DateTime ExamOpen = new(2026, 9, 10, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExamClose = new(2026, 9, 17, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MorningOpen = new(2026, 9, 15, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MorningClose = new(2026, 9, 15, 2, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime AfternoonOpen = new(2026, 9, 16, 7, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AfternoonClose = new(2026, 9, 16, 8, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Assigned = new(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc);

    private static Exam BuildExam(bool isDraft = false) => new()
    {
        Id = 1,
        Title = "De chung",
        DurationMinutes = 45,
        TotalQuestions = 9,
        EasyPercentage = 45,
        MediumPercentage = 33,
        HardPercentage = 22,
        PointPerQuestion = 1,
        OpenAt = ExamOpen,
        CloseAt = ExamClose,
        IsDraft = isDraft,
        CreatedByUserId = 2
    };

    private static AssignmentWindowRow Row(
        int? groupId,
        DateTime? openAt,
        DateTime? closeAt,
        int count = 1,
        bool started = false,
        int examId = 1) =>
        new(examId, groupId, groupId is null ? null : $"12A{groupId}", openAt, closeAt, started, count, Assigned);

    [Fact]
    public void Moi_lop_mot_dong_voi_gio_that_dang_ap()
    {
        var windows = AssignmentWindowSummary.Build(
            BuildExam(),
            [Row(1, MorningOpen, MorningClose, count: 30), Row(2, null, null, count: 28)],
            asOfUtc: MorningOpen.AddMinutes(10));

        windows.Should().HaveCount(2);
        windows[0].GroupName.Should().Be("12A1");
        windows[0].OpenAt.Should().Be(MorningOpen);
        windows[0].HasScheduleOverride.Should().BeTrue();
        windows[0].Lifecycle.Should().Be("open");
        windows[0].AssignedCount.Should().Be(30);

        windows[1].GroupName.Should().Be("12A2");
        windows[1].OpenAt.Should().Be(ExamOpen);
        windows[1].CloseAt.Should().Be(ExamClose);
        windows[1].HasScheduleOverride.Should().BeFalse();
    }

    [Fact]
    public void Lich_chung_da_dong_nhung_lop_van_mo()
    {
        var exam = BuildExam();
        exam.CloseAt = MorningOpen.AddDays(-1);
        exam.OpenAt = MorningOpen.AddDays(-3);

        var windows = AssignmentWindowSummary.Build(exam, [Row(1, MorningOpen, MorningClose)], MorningClose);

        // Bằng giờ đóng vẫn còn mở — cùng phép so với phía học sinh.
        windows.Single().Lifecycle.Should().Be("open");
    }

    [Fact]
    public void Lop_da_doi_gio_hien_gio_moi_du_so_dong_em_da_lam_theo_gio_cu()
    {
        var windows = AssignmentWindowSummary.Build(
            BuildExam(),
            [
                Row(1, MorningOpen, MorningClose, count: 25, started: true),
                Row(1, AfternoonOpen, AfternoonClose, count: 5)
            ],
            asOfUtc: MorningClose.AddHours(1));

        var window = windows.Single();
        window.OpenAt.Should().Be(AfternoonOpen);
        window.Lifecycle.Should().Be("scheduled");
        window.AssignedCount.Should().Be(30);
        window.StartedCount.Should().Be(25);
    }

    [Fact]
    public void Hoc_sinh_chon_le_gom_theo_gio_da_phan_giai()
    {
        var windows = AssignmentWindowSummary.Build(
            BuildExam(),
            [
                Row(null, null, null, count: 2),
                // Giờ riêng trùng khít lịch đề: cùng một khung với hai em trên, không tách dòng.
                Row(null, ExamOpen, ExamClose, count: 1),
                Row(null, AfternoonOpen, AfternoonClose, count: 1, started: true)
            ],
            asOfUtc: ExamOpen.AddHours(1));

        windows.Should().HaveCount(2);
        windows.Should().OnlyContain(window => window.GroupId == null && window.GroupName == null);
        windows[0].OpenAt.Should().Be(ExamOpen);
        windows[0].AssignedCount.Should().Be(3);
        windows[1].OpenAt.Should().Be(AfternoonOpen);
        windows[1].StartedCount.Should().Be(1);
    }

    [Fact]
    public void Lop_dung_truoc_hoc_sinh_chon_le()
    {
        var windows = AssignmentWindowSummary.Build(
            BuildExam(),
            [Row(null, MorningOpen, MorningClose), Row(3, null, null), Row(1, null, null)],
            asOfUtc: ExamOpen);

        windows.Select(window => window.GroupName).Should().Equal("12A1", "12A3", null);
    }

    [Fact]
    public void De_nhap_thi_moi_khung_deu_la_nhap()
    {
        var windows = AssignmentWindowSummary.Build(
            BuildExam(isDraft: true),
            [Row(1, MorningOpen, MorningClose)],
            asOfUtc: MorningOpen.AddMinutes(5));

        windows.Single().Lifecycle.Should().Be("draft");
    }

    [Fact]
    public void Chi_lay_dong_cua_dung_de_nay()
    {
        var windows = AssignmentWindowSummary.Build(
            BuildExam(),
            [Row(1, MorningOpen, MorningClose, examId: 2)],
            asOfUtc: ExamOpen);

        windows.Should().BeEmpty();
    }
}
