using Anthill.Application.Common;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Assignments;

/// <summary>
/// Ai được báo khi giờ làm bài đổi, và câu chữ nói gì.
/// </summary>
/// <remarks>
/// Đây là phần dễ làm sai nhất của thông báo đổi lịch: báo cho cả những em không bị ảnh hưởng thì học sinh
/// quen tay bỏ qua loại thông báo này, còn bỏ sót một em là em đó đến muộn giờ thi.
/// </remarks>
public class ScheduleNoticeTests
{
    private static readonly DateTime Now = new(2026, 9, 14, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExamOpen = new(2026, 9, 14, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExamClose = new(2026, 9, 20, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ClassOpen = new(2026, 9, 15, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ClassClose = new(2026, 9, 15, 2, 30, 0, DateTimeKind.Utc);

    private static Exam BuildExam(DateTime? openAt = null, DateTime? closeAt = null, int durationMinutes = 45) => new()
    {
        Id = 1,
        Title = "Kiểm tra giữa kỳ",
        DurationMinutes = durationMinutes,
        TotalQuestions = 9,
        EasyPercentage = 45,
        MediumPercentage = 33,
        HardPercentage = 22,
        PointEasy = 1,
        PointMedium = 1,
        PointHard = 1,
        OpenAt = openAt ?? ExamOpen,
        CloseAt = closeAt ?? ExamClose,
        IsDraft = false,
        CreatedByUserId = 2
    };

    private static ExamAssignment Row(DateTime? openAt = null, DateTime? closeAt = null) => new()
    {
        Id = 7,
        ExamId = 1,
        StudentId = 3,
        OpenAt = openAt,
        CloseAt = closeAt
    };

    private static AssignmentSchedule.ExamTiming Timing(Exam exam) => AssignmentSchedule.ExamTiming.Of(exam);

    [Fact]
    public void Doi_lich_chung_bao_cho_em_dang_theo_lich_chung()
    {
        var before = Timing(BuildExam());
        var after = BuildExam(closeAt: ExamClose.AddDays(1));

        var notice = ScheduleNotice.FromExamEdit(Row(), before, after, Now);

        notice.WindowChanged.Should().BeTrue();
        notice.DurationChanged.Should().BeFalse();
    }

    [Fact]
    public void Doi_lich_chung_khong_dong_toi_em_co_gio_rieng()
    {
        // Lớp đã được đặt khung giờ riêng thì lịch chung chỉ còn là mặc định cho các lớp khác.
        var before = Timing(BuildExam());
        var after = BuildExam(openAt: ExamOpen.AddDays(2), closeAt: ExamClose.AddDays(2));

        var notice = ScheduleNotice.FromExamEdit(Row(ClassOpen, ClassClose), before, after, Now);

        notice.HasChange.Should().BeFalse();
    }

    [Fact]
    public void Doi_thoi_luong_bao_cho_moi_em_ke_ca_em_co_gio_rieng()
    {
        var before = Timing(BuildExam());
        var after = BuildExam(durationMinutes: 60);

        var notice = ScheduleNotice.FromExamEdit(Row(ClassOpen, ClassClose), before, after, Now);

        notice.WindowChanged.Should().BeFalse();
        notice.DurationChanged.Should().BeTrue();
    }

    [Fact]
    public void Sua_ten_de_khong_sinh_thong_bao_nao()
    {
        var before = Timing(BuildExam());

        ScheduleNotice.FromExamEdit(Row(), before, BuildExam(), Now).HasChange.Should().BeFalse();
    }

    [Fact]
    public void De_con_la_nhap_thi_khong_bao_gi()
    {
        // Nháp thì không em nào vào được, nên lịch của nó chưa hứa hẹn điều gì với ai.
        var before = Timing(BuildExam());
        var after = BuildExam(closeAt: ExamClose.AddDays(1));
        after.IsDraft = true;

        ScheduleNotice.FromExamEdit(Row(), before, after, Now).HasChange.Should().BeFalse();
    }

    [Fact]
    public void Sua_lich_mot_de_da_dong_tu_lau_khong_danh_thuc_ai()
    {
        // Cả khung cũ lẫn khung mới đều nằm ở quá khứ: em không làm được bài trước lần sửa, và vẫn không
        // làm được sau lần sửa. Báo ở đây chỉ là tiếng ồn.
        var before = Timing(BuildExam(openAt: Now.AddDays(-9), closeAt: Now.AddDays(-8)));
        var after = BuildExam(openAt: Now.AddDays(-7), closeAt: Now.AddDays(-6));

        ScheduleNotice.FromExamEdit(Row(), before, after, Now).HasChange.Should().BeFalse();
    }

    [Fact]
    public void Mo_lai_mot_de_da_dong_thi_co_bao()
    {
        var before = Timing(BuildExam(openAt: Now.AddDays(-9), closeAt: Now.AddDays(-8)));
        var after = BuildExam(openAt: Now.AddHours(1), closeAt: Now.AddHours(3));

        ScheduleNotice.FromExamEdit(Row(), before, after, Now).WindowChanged.Should().BeTrue();
    }

    [Fact]
    public void Dong_som_mot_de_dang_mo_thi_co_bao()
    {
        var before = Timing(BuildExam(openAt: Now.AddHours(-1), closeAt: Now.AddHours(5)));
        var after = BuildExam(openAt: Now.AddHours(-1), closeAt: Now.AddMinutes(-1));

        ScheduleNotice.FromExamEdit(Row(), before, after, Now).WindowChanged.Should().BeTrue();
    }

    [Fact]
    public void Doi_khung_gio_cua_lop_thi_bao_cho_em_cua_lop()
    {
        var exam = BuildExam();
        var row = Row(ClassOpen, ClassClose);
        var change = new AssignmentSchedule.RescheduleChange(row, PreviousOpenAt: null, PreviousCloseAt: null);

        var notice = ScheduleNotice.FromReschedule(change, exam, Now);

        notice.WindowChanged.Should().BeTrue();
        notice.DurationChanged.Should().BeFalse();
    }

    [Fact]
    public void Dat_gio_rieng_trung_khit_lich_de_khong_bao_gi()
    {
        var exam = BuildExam();
        var row = Row(ExamOpen, ExamClose);
        var change = new AssignmentSchedule.RescheduleChange(row, PreviousOpenAt: null, PreviousCloseAt: null);

        ScheduleNotice.FromReschedule(change, exam, Now).HasChange.Should().BeFalse();
    }

    [Fact]
    public void Doi_giua_hai_khung_da_qua_khong_bao_gi()
    {
        var exam = BuildExam();
        var row = Row(Now.AddDays(-3), Now.AddDays(-2));
        var change = new AssignmentSchedule.RescheduleChange(row, Now.AddDays(-5), Now.AddDays(-4));

        ScheduleNotice.FromReschedule(change, exam, Now).HasChange.Should().BeFalse();
    }

    [Fact]
    public void Cau_chu_noi_dung_thu_vua_doi()
    {
        var exam = BuildExam(durationMinutes: 60);
        var row = Row();

        var window = ScheduleNoticeText.ExamUpdated(new ScheduleNotice(row, WindowChanged: true, DurationChanged: false), exam, Now);
        var duration = ScheduleNoticeText.ExamUpdated(new ScheduleNotice(row, WindowChanged: false, DurationChanged: true), exam, Now);
        var both = ScheduleNoticeText.ExamUpdated(new ScheduleNotice(row, WindowChanged: true, DurationChanged: true), exam, Now);

        window.Message.Should().Contain("đổi giờ làm bài").And.NotContain("phút");
        duration.Message.Should().Contain("60 phút").And.NotContain("giờ mở");
        both.Message.Should().Contain("đổi giờ làm bài").And.Contain("60 phút");

        // Thông báo dẫn tới trang của chính lượt giao này, không tới danh sách chung: em cần thấy giờ mới
        // của mình, và giờ đó chỉ có trên trang giới thiệu bài.
        window.Link.Should().Be("/hoc-sinh/bai-kiem-tra/7");
        window.Type.Should().Be(NotificationType.ExamUpdated);
        window.RecipientUserId.Should().Be(3);
        window.TargetId.Should().Be(7);
    }
}
