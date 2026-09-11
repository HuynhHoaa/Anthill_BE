using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Assignments;

/// <summary>
/// Trang thai "expired" phai tinh theo khung gio CUA EM, cung phep tinh voi Lifecycle nam canh no
/// tren cung mot DTO. Truoc day hai truong nay doc hai lich khac nhau va noi nguoc nhau.
/// </summary>
public class AssignmentStatusTests
{
    private static readonly DateTime ExamOpen = new(2026, 9, 10, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExamClose = new(2026, 9, 17, 1, 0, 0, DateTimeKind.Utc);

    private static Exam BuildExam(DateTime openAt, DateTime closeAt, bool isDraft = false) => new()
    {
        Id = 1,
        Title = "De chung",
        DurationMinutes = 45,
        TotalQuestions = 9,
        OpenAt = openAt,
        CloseAt = closeAt,
        IsDraft = isDraft,
        CreatedByUserId = 2,
        CreatedBy = new User
        {
            FullName = "Nguyen Van An",
            Email = "an.nv@anthill.edu.vn",
            Username = "an.nv",
            PasswordHash = "hash",
            SecurityStamp = "stamp",
            AvatarColor = "#2563eb"
        }
    };

    private static ExamAssignment BuildAssignment(Exam exam, DateTime? openAt, DateTime? closeAt) => new()
    {
        Id = 10,
        ExamId = exam.Id,
        StudentId = 3,
        Exam = exam,
        OpenAt = openAt,
        CloseAt = closeAt
    };

    [Fact]
    public void Lop_lo_khung_gio_rieng_thi_expired_du_lich_chung_con_mo()
    {
        var assignment = BuildAssignment(
            BuildExam(ExamOpen, ExamClose),
            new DateTime(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 11, 3, 0, 0, DateTimeKind.Utc));
        var asOf = new DateTime(2026, 9, 11, 4, 0, 0, DateTimeKind.Utc);

        assignment.ResolveStatus(asOf).Should().Be(AssignmentStatus.Expired);
        assignment.ResolveLifecycle(asOf).Should().Be(ExamLifecycle.Closed);
        assignment.ToDto(asOf).Status.Should().Be("expired");
    }

    [Fact]
    public void Dang_trong_khung_gio_rieng_thi_assigned_du_lich_chung_da_dong()
    {
        var assignment = BuildAssignment(
            BuildExam(ExamOpen, new DateTime(2026, 9, 11, 1, 0, 0, DateTimeKind.Utc)),
            new DateTime(2026, 9, 12, 7, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 12, 8, 0, 0, DateTimeKind.Utc));
        var asOf = new DateTime(2026, 9, 12, 7, 30, 0, DateTimeKind.Utc);

        assignment.ResolveStatus(asOf).Should().Be(AssignmentStatus.Assigned);

        var item = assignment.ToStudentExamItemDto(asOf);
        item.Status.Should().Be("assigned");
        item.Lifecycle.Should().Be("open");
        item.CanStart.Should().BeTrue();
    }

    /// <summary>Khung rieng 20/9 07:00-08:00, nam han ngoai lich chung cua de.</summary>
    [Theory]
    [InlineData(2026, 9, 20, 6, 0, 0)]
    [InlineData(2026, 9, 20, 7, 30, 0)]
    [InlineData(2026, 9, 20, 8, 0, 0)]
    [InlineData(2026, 9, 20, 8, 0, 1)]
    public void Status_va_Lifecycle_tren_cung_DTO_khong_mau_thuan(int y, int mo, int d, int h, int mi, int s)
    {
        var assignment = BuildAssignment(
            BuildExam(ExamOpen, ExamClose),
            new DateTime(2026, 9, 20, 7, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc));
        var asOf = new DateTime(y, mo, d, h, mi, s, DateTimeKind.Utc);

        var item = assignment.ToStudentExamItemDto(asOf);

        (item.Status == "expired").Should().Be(item.Lifecycle == "closed");
        if (item.Status == "expired")
        {
            item.CanStart.Should().BeFalse();
        }
    }

    [Fact]
    public void Dung_giay_dong_cua_khung_rieng_van_con_assigned()
    {
        var closeAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);
        var assignment = BuildAssignment(BuildExam(ExamOpen, ExamClose), closeAt.AddHours(-1), closeAt);

        assignment.ResolveStatus(closeAt).Should().Be(AssignmentStatus.Assigned);
        assignment.ToStudentExamItemDto(closeAt).Lifecycle.Should().Be("open");
    }

    [Fact]
    public void Ban_nhap_khong_bao_gio_expired()
    {
        var assignment = BuildAssignment(
            BuildExam(ExamOpen, ExamClose, isDraft: true),
            new DateTime(2026, 9, 1, 1, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 1, 2, 0, 0, DateTimeKind.Utc));

        assignment.ResolveStatus(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc))
            .Should().Be(AssignmentStatus.Assigned);
    }

    [Fact]
    public void Co_luot_lam_bai_thi_lay_trang_thai_luot_ke_ca_khi_thieu_Exam()
    {
        var assignment = new ExamAssignment
        {
            ExamId = 1,
            StudentId = 3,
            Exam = null!,
            Attempt = new ExamAttempt { Status = AssignmentStatus.Graded }
        };

        var act = () => assignment.ResolveStatus(ExamClose);

        act.Should().NotThrow().Which.Should().Be(AssignmentStatus.Graded);
    }

    [Fact]
    public void Trang_thai_thieu_Exam_ma_chua_co_luot_thi_nem_loi_ro_rang()
    {
        var assignment = new ExamAssignment { ExamId = 1, StudentId = 3 };

        var act = () => assignment.ResolveStatus(ExamClose);

        act.Should().Throw<InvalidOperationException>().WithMessage("*chưa được nạp*");
    }

    [Fact]
    public void DTO_giao_vien_mang_khung_gio_that_cua_em()
    {
        var overrideOpen = new DateTime(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc);
        var overrideClose = new DateTime(2026, 9, 11, 3, 0, 0, DateTimeKind.Utc);
        var withOverride = BuildAssignment(BuildExam(ExamOpen, ExamClose), overrideOpen, overrideClose);
        withOverride.AssignedViaGroupId = 5;
        var plain = BuildAssignment(BuildExam(ExamOpen, ExamClose), null, null);

        var a = withOverride.ToDto(ExamOpen);
        a.OpenAt.Should().Be(overrideOpen);
        a.CloseAt.Should().Be(overrideClose);
        a.HasScheduleOverride.Should().BeTrue();
        a.AssignedViaGroupId.Should().Be(5);

        var b = plain.ToDto(ExamOpen);
        b.OpenAt.Should().Be(ExamOpen);
        b.CloseAt.Should().Be(ExamClose);
        b.OpenAt.Kind.Should().Be(DateTimeKind.Utc);
        b.HasScheduleOverride.Should().BeFalse();
        b.AssignedViaGroupId.Should().BeNull();
    }
}
