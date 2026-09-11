using Anthill.Application.Mappings;
using Anthill.Application.Services;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Assignments;

/// <summary>
/// Trang tong quan cua hoc sinh chia bai vao "dang mo" va "sap toi" theo khung gio CUA EM, dung nhu
/// the bai ben trong. Doc lich cua de o day la de trang noi nguoc voi the no dang ve.
/// </summary>
public class StudentDashboardBucketsTests
{
    private static readonly DateTime AsOf = new(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int day, int hour) => new(2026, 9, day, hour, 0, 0, DateTimeKind.Utc);

    private static ExamAssignment Build(
        int id,
        DateTime examOpen,
        DateTime examClose,
        DateTime? openAt = null,
        DateTime? closeAt = null,
        AssignmentStatus? attempt = null,
        bool isDraft = false)
    {
        var exam = new Exam
        {
            Id = id,
            Title = $"De {id}",
            DurationMinutes = 45,
            TotalQuestions = 9,
            IsDraft = isDraft,
            OpenAt = examOpen,
            CloseAt = examClose,
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

        return new ExamAssignment
        {
            Id = id * 10,
            ExamId = id,
            StudentId = 5,
            Exam = exam,
            OpenAt = openAt,
            CloseAt = closeAt,
            Attempt = attempt is { } status
                ? new ExamAttempt
                {
                    Id = id * 100,
                    Status = status,
                    StartedAt = AsOf.AddMinutes(-10),
                    ExpiresAt = AsOf.AddMinutes(30)
                }
                : null
        };
    }

    [Fact]
    public void Lop_dang_trong_gio_rieng_nam_o_dang_mo_du_lich_chung_da_dong()
    {
        var a = Build(1, At(1, 0), At(3, 0), At(10, 7), At(10, 11));

        var (open, upcoming) = DashboardService.SplitOpenAndUpcoming([a], AsOf);

        open.Should().ContainSingle().Which.Should().BeSameAs(a);
        upcoming.Should().BeEmpty();
    }

    [Fact]
    public void Lop_dang_trong_gio_rieng_khong_bi_xep_vao_sap_toi()
    {
        var a = Build(1, At(12, 0), At(13, 0), At(10, 7), At(10, 11));

        var (open, upcoming) = DashboardService.SplitOpenAndUpcoming([a], AsOf);

        open.Should().ContainSingle();
        upcoming.Should().BeEmpty();
    }

    [Fact]
    public void Lop_da_het_gio_rieng_khong_con_o_dang_mo()
    {
        var a = Build(1, At(1, 0), At(20, 0), At(10, 3), At(10, 5));

        var (open, upcoming) = DashboardService.SplitOpenAndUpcoming([a], AsOf);

        open.Should().BeEmpty();
        upcoming.Should().BeEmpty();
    }

    [Fact]
    public void Lop_chua_toi_gio_rieng_nam_o_sap_toi_du_lich_chung_dang_mo()
    {
        var a = Build(1, At(1, 0), At(20, 0), At(10, 14), At(10, 15));

        var (open, upcoming) = DashboardService.SplitOpenAndUpcoming([a], AsOf);

        open.Should().BeEmpty();
        upcoming.Should().ContainSingle().Which.Should().BeSameAs(a);
    }

    [Fact]
    public void Dang_mo_sap_theo_gio_dong_cua_lop_khong_theo_de()
    {
        // A: de dong 20/9 nhung lop dong luc 09:00 hom nay. B: de dong 10:00, khong co gio rieng.
        var a = Build(1, At(1, 0), At(20, 0), At(10, 7), At(10, 9));
        var b = Build(2, At(1, 0), At(10, 10));

        var (open, _) = DashboardService.SplitOpenAndUpcoming([b, a], AsOf);

        open.Select(x => x.ExamId).Should().Equal(1, 2);
    }

    [Fact]
    public void Sap_toi_sap_theo_gio_mo_cua_lop()
    {
        var a = Build(1, At(9, 0), At(20, 0), At(10, 12), At(10, 13));
        var b = Build(2, At(10, 11), At(20, 0));

        var (_, upcoming) = DashboardService.SplitOpenAndUpcoming([a, b], AsOf);

        upcoming.Select(x => x.ExamId).Should().Equal(2, 1);
    }

    [Fact]
    public void Bai_da_nop_bi_loai_bai_dang_lam_van_o_dang_mo()
    {
        var graded = Build(1, At(1, 0), At(20, 0), attempt: AssignmentStatus.Graded);
        var running = Build(2, At(1, 0), At(20, 0), At(10, 7), At(10, 9), attempt: AssignmentStatus.InProgress);

        var (open, upcoming) = DashboardService.SplitOpenAndUpcoming([graded, running], AsOf);

        open.Select(x => x.ExamId).Should().Equal(2);
        upcoming.Should().BeEmpty();
    }

    [Fact]
    public void De_nhap_khong_vao_danh_sach_nao_du_gio_rieng_bao_trum_hien_tai()
    {
        var a = Build(1, At(1, 0), At(20, 0), At(10, 7), At(10, 11), isDraft: true);

        var (open, upcoming) = DashboardService.SplitOpenAndUpcoming([a], AsOf);

        open.Should().BeEmpty();
        upcoming.Should().BeEmpty();
    }

    /// <summary>Tinh chat ma ca nhom test nay canh: danh sach va the bai ben trong khong bao gio noi nguoc nhau.</summary>
    [Fact]
    public void Moi_the_trong_dang_mo_deu_open_moi_the_trong_sap_toi_deu_scheduled()
    {
        var all = new[]
        {
            Build(1, At(1, 0), At(3, 0), At(10, 7), At(10, 11)),
            Build(2, At(12, 0), At(13, 0), At(10, 7), At(10, 11)),
            Build(3, At(1, 0), At(20, 0), At(10, 3), At(10, 5)),
            Build(4, At(1, 0), At(20, 0), At(10, 14), At(10, 15)),
            Build(5, At(1, 0), At(10, 10)),
            Build(6, At(11, 0), At(12, 0), At(9, 0), At(9, 2)),
            Build(7, At(1, 0), At(20, 0), At(10, 7), At(10, 9), attempt: AssignmentStatus.InProgress)
        };

        var (open, upcoming) = DashboardService.SplitOpenAndUpcoming(all, AsOf);

        open.Should().NotBeEmpty();
        upcoming.Should().NotBeEmpty();
        open.Should().OnlyContain(x => x.ToStudentExamItemDto(AsOf).Lifecycle == "open");
        upcoming.Should().OnlyContain(x => x.ToStudentExamItemDto(AsOf).Lifecycle == "scheduled");
    }
}
