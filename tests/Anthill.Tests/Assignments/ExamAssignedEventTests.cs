using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using FluentAssertions;

namespace Anthill.Tests.Assignments;

/// <summary>
/// Su kien realtime luc giao bai phai hua dung gio ma the bai cua em se dem nguoc — lich rieng cua
/// lop neu co. Truoc day no mang lich chung cua de.
/// </summary>
public class ExamAssignedEventTests
{
    private static readonly DateTime ExamOpen = new(2026, 9, 10, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExamClose = new(2026, 9, 17, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OverrideOpen = new(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OverrideClose = new(2026, 9, 11, 5, 0, 0, DateTimeKind.Utc);

    private static Exam BuildExam() => new()
    {
        Id = 1,
        Title = "De chung",
        DurationMinutes = 45,
        TotalQuestions = 9,
        OpenAt = ExamOpen,
        CloseAt = ExamClose,
        CreatedByUserId = 2,
        CreatedBy = new User
        {
            FullName = "Nguyễn Văn An",
            Email = "an.nv@anthill.edu.vn",
            Username = "an.nv",
            PasswordHash = "hash",
            SecurityStamp = "stamp",
            AvatarColor = "#2563eb"
        }
    };

    [Fact]
    public void Giao_kem_gio_rieng_thi_su_kien_mang_gio_rieng()
    {
        // Khong gan navigation Exam: ban ghi vua tao trong request chua chac da co no.
        var assignment = new ExamAssignment
        {
            Id = 10, ExamId = 1, StudentId = 3, OpenAt = OverrideOpen, CloseAt = OverrideClose
        };

        var payload = assignment.ToAssignedEvent(BuildExam(), "Nguyễn Văn An");

        payload.OpenAt.Should().Be(OverrideOpen);
        payload.CloseAt.Should().Be(OverrideClose);
        payload.AssignmentId.Should().Be(10);
        payload.ExamId.Should().Be(1);
        payload.TeacherName.Should().Be("Nguyễn Văn An");
        payload.DurationMinutes.Should().Be(45);
    }

    [Fact]
    public void Giao_khong_kem_gio_rieng_va_chua_nap_Exam_thi_van_ra_lich_chung_khong_nem_loi()
    {
        var assignment = new ExamAssignment { Id = 10, ExamId = 1, StudentId = 3 };

        var act = () => assignment.ToAssignedEvent(BuildExam(), "GV");

        var payload = act.Should().NotThrow().Subject;
        payload.OpenAt.Should().Be(ExamOpen);
        payload.CloseAt.Should().Be(ExamClose);
    }

    [Fact]
    public void Su_kien_trung_gio_voi_the_bai_cua_hoc_sinh()
    {
        var exam = BuildExam();
        var assignment = new ExamAssignment
        {
            Id = 10, ExamId = 1, StudentId = 3, Exam = exam, OpenAt = OverrideOpen, CloseAt = OverrideClose
        };

        var payload = assignment.ToAssignedEvent(exam, "GV");
        var card = assignment.ToStudentExamItemDto(new DateTime(2026, 9, 11, 3, 0, 0, DateTimeKind.Utc));

        payload.OpenAt.Should().Be(card.OpenAt);
        payload.CloseAt.Should().Be(card.CloseAt);
    }

    [Fact]
    public void Moc_khong_ghi_Kind_van_ra_UTC()
    {
        var assignment = new ExamAssignment
        {
            Id = 10,
            ExamId = 1,
            StudentId = 3,
            OpenAt = DateTime.SpecifyKind(OverrideOpen, DateTimeKind.Unspecified),
            CloseAt = DateTime.SpecifyKind(OverrideClose, DateTimeKind.Unspecified)
        };

        var payload = assignment.ToAssignedEvent(BuildExam(), "GV");

        payload.OpenAt.Kind.Should().Be(DateTimeKind.Utc);
        payload.CloseAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Su_kien_sua_de_cung_mang_khung_gio_that_cua_em()
    {
        var exam = BuildExam();
        var withOverride = new ExamAssignment
        {
            Id = 10, ExamId = 1, StudentId = 3, OpenAt = OverrideOpen, CloseAt = OverrideClose
        };
        var plain = new ExamAssignment { Id = 11, ExamId = 1, StudentId = 4 };

        withOverride.ToUpdatedEvent(exam).CloseAt.Should().Be(OverrideClose);
        plain.ToUpdatedEvent(exam).CloseAt.Should().Be(ExamClose);
        plain.ToUpdatedEvent(exam).AssignmentId.Should().Be(11);
    }
}
