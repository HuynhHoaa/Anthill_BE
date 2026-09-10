using EduQuiz.Application.Common;
using EduQuiz.Domain.Entities;
using FluentAssertions;

namespace EduQuiz.Tests.Assignments;

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
        PointPerQuestion = 1,
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
    /// Dat mot nua la trang thai vo nghia. Quay ve lich chung con hon ghep nua no voi nua kia va
    /// tao ra mot khung gio khong ai co y dinh dat ra.
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

        // Nua vang mat lay tu de; nua co dat van duoc ton trong.
        (hasOpen ? openAt : closeAt).Should().Be(hasOpen ? assignment.OpenAt!.Value : assignment.CloseAt!.Value);
        AssignmentSchedule.HasOverride(assignment).Should().BeFalse();
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
