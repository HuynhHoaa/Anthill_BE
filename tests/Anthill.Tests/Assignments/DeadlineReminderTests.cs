using Anthill.Application.Common;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Assignments;

/// <summary>
/// Lúc nào thì nhắc một em chưa vào thi rằng giờ của em sắp hết.
/// </summary>
public class DeadlineReminderTests
{
    private const int Duration = 45;
    private const int Margin = 30;

    private static readonly DateTime Now = new(2026, 9, 14, 3, 0, 0, DateTimeKind.Utc);

    private static DeadlineReminder.Decision Decide(
        double opensHoursAgo,
        double closesInHours,
        DateTime? alreadyNotified = null,
        int duration = Duration) =>
        DeadlineReminder.Decide(
            Now.AddHours(-opensHoursAgo),
            Now.AddHours(closesInHours),
            duration,
            Margin,
            alreadyNotified,
            Now);

    [Fact]
    public void Nhac_khi_chi_con_vua_du_gio_lam_bai_cong_bien()
    {
        // Mốc nhắc là 75 phút trước giờ đóng: 45 phút làm bài cộng 30 phút xoay xở.
        Decide(opensHoursAgo: 2, closesInHours: 1).ShouldSend.Should().BeTrue();
        Decide(opensHoursAgo: 2, closesInHours: 1).MinutesRemaining.Should().Be(60);
    }

    [Fact]
    public void Chua_toi_moc_thi_chua_nhac()
    {
        Decide(opensHoursAgo: 2, closesInHours: 3).ShouldSend.Should().BeFalse();
    }

    [Fact]
    public void Bai_chua_mo_hay_da_dong_thi_khong_nhac()
    {
        // Chưa mở: "sắp hết giờ" là câu vô nghĩa với một bài em còn chưa vào được.
        DeadlineReminder.Decide(Now.AddMinutes(30), Now.AddHours(1), Duration, Margin, null, Now)
            .ShouldSend.Should().BeFalse();

        Decide(opensHoursAgo: 5, closesInHours: -0.1).ShouldSend.Should().BeFalse();
    }

    [Fact]
    public void Khung_gio_vua_khit_mot_tiet_hoc_thi_khong_nhac()
    {
        // Lớp làm bài 45 phút trong một khung 60 phút: mốc nhắc rơi vào trước cả giờ mở, nên lời nhắc sẽ
        // bật ra ngay khi em vừa nhận bài. Buổi đó có giáo viên đứng lớp rồi.
        DeadlineReminder.Decide(Now.AddMinutes(-5), Now.AddMinutes(55), Duration, Margin, null, Now)
            .ShouldSend.Should().BeFalse();
    }

    [Fact]
    public void Da_nhac_cho_dung_khung_nay_thi_thoi()
    {
        var closeAt = Now.AddHours(1);

        DeadlineReminder.Decide(Now.AddHours(-2), closeAt, Duration, Margin, closeAt, Now)
            .ShouldSend.Should().BeFalse();
    }

    [Fact]
    public void Doi_khung_sau_khi_da_nhac_thi_nhac_lai_theo_gio_moi()
    {
        // Giáo viên gia hạn thêm mười phút: mốc đã nhắc không còn khớp giờ đóng, nên em được nhắc lại đúng
        // một lần nữa theo khung mới. Giữ một cờ đã-nhắc thay vì mốc thì em không bao giờ nghe thêm gì.
        var notifiedFor = Now.AddHours(1);

        DeadlineReminder.Decide(Now.AddHours(-2), Now.AddMinutes(70), Duration, Margin, notifiedFor, Now)
            .ShouldSend.Should().BeTrue();

        // Gia hạn xa hơn mốc nhắc thì em chưa nghe gì thêm cho tới khi giờ mới cũng gần hết.
        DeadlineReminder.Decide(Now.AddHours(-2), Now.AddMinutes(90), Duration, Margin, notifiedFor, Now)
            .ShouldSend.Should().BeFalse();
    }

    [Fact]
    public void Moc_nhac_di_theo_thoi_luong_cua_de()
    {
        // Bài ba tiếng được nhắc sớm hơn nhiều so với bài mười lăm phút, vì "còn kịp làm bài" của hai bài
        // đó là hai thời điểm khác hẳn nhau.
        DeadlineReminder.LeadTime(180, Margin).Should().Be(TimeSpan.FromMinutes(210));
        DeadlineReminder.LeadTime(15, Margin).Should().Be(TimeSpan.FromMinutes(45));

        Decide(opensHoursAgo: 6, closesInHours: 2.5, duration: 180).ShouldSend.Should().BeTrue();
        Decide(opensHoursAgo: 6, closesInHours: 2.5, duration: 15).ShouldSend.Should().BeFalse();
    }

    [Fact]
    public void Cau_chu_doi_theo_viec_em_con_kip_lam_tron_bai_hay_khong()
    {
        var exam = new Exam
        {
            Id = 1,
            Title = "Kiểm tra 15 phút",
            DurationMinutes = 45,
            TotalQuestions = 9,
            EasyPercentage = 45,
            MediumPercentage = 33,
            HardPercentage = 22,
            PointEasy = 1,
            PointMedium = 1,
            PointHard = 1,
            OpenAt = Now.AddHours(-2),
            CloseAt = Now.AddHours(1),
            IsDraft = false,
            CreatedByUserId = 2
        };
        var assignment = new ExamAssignment { Id = 7, ExamId = 1, StudentId = 3 };

        var roomy = DeadlineReminderText.For(assignment, exam, 60, Now);
        var tight = DeadlineReminderText.For(assignment, exam, 20, Now);

        roomy.Message.Should().Contain("60 phút").And.Contain("Hãy vào làm trước khi hết giờ.");
        tight.Message.Should().Contain("không còn đủ 45 phút");
        roomy.Type.Should().Be(NotificationType.DeadlineSoon);
        roomy.Link.Should().Be("/hoc-sinh/bai-kiem-tra/7");
        roomy.RecipientUserId.Should().Be(3);
    }
}
