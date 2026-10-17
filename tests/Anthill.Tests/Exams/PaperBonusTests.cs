using Anthill.Application.Common;
using Anthill.Application.DTOs.Exams;
using Anthill.Application.Validators;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Exams;

/// <summary>Điểm cộng thêm để mỗi đề nhỏ cộng đúng 10 khi điểm theo mức (hai chữ số thập phân) không chia ra đúng.</summary>
public class PaperBonusTests
{
    private static readonly DateTime OpenAt = new(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);

    private static (int QuestionId, QuestionDifficulty Difficulty) Q(int id, QuestionDifficulty level) => (id, level);

    // ---- chọn câu nhận điểm cộng

    [Fact]
    public void Khong_cong_gi_khi_so_don_vi_la_0()
    {
        var ordered = new[] { Q(1, QuestionDifficulty.Easy), Q(2, QuestionDifficulty.Hard) };

        PaperBonus.Recipients(ordered, 0).Should().BeEmpty();
    }

    [Fact]
    public void Cong_cho_cau_kho_truoc_theo_thu_tu_trong_ban_de()
    {
        var ordered = new[]
        {
            Q(1, QuestionDifficulty.Easy),
            Q(2, QuestionDifficulty.Hard),
            Q(3, QuestionDifficulty.Medium),
            Q(4, QuestionDifficulty.Hard),
            Q(5, QuestionDifficulty.Hard)
        };

        PaperBonus.Recipients(ordered, 2).Should().BeEquivalentTo(new[] { 2, 4 });
    }

    [Fact]
    public void Het_cau_kho_thi_tran_xuong_cau_trung_binh_roi_de()
    {
        var ordered = new[]
        {
            Q(1, QuestionDifficulty.Easy),
            Q(2, QuestionDifficulty.Hard),
            Q(3, QuestionDifficulty.Medium),
            Q(4, QuestionDifficulty.Medium)
        };

        PaperBonus.Recipients(ordered, 3).Should().BeEquivalentTo(new[] { 2, 3, 4 });
        PaperBonus.Recipients(ordered, 4).Should().BeEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    [Fact]
    public void Moi_cau_nhan_nhieu_nhat_mot_don_vi_ke_ca_khi_don_vi_nhieu_hon_so_cau()
    {
        var ordered = new[] { Q(1, QuestionDifficulty.Hard), Q(2, QuestionDifficulty.Easy) };

        PaperBonus.Recipients(ordered, 9).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void Tong_diem_de_nho_voi_diem_cong_dung_10()
    {
        // 3 / 3 / 3 câu, điểm 0,9 / 1,1 / 1,33 → 9,99; cộng 1 đơn vị 0,01 → 10,00 trên mọi bản đề.
        var baseTotal = ExamScoring.TotalOf(9, 33, 33, 0.9m, 1.1m, 1.33m);

        baseTotal.Should().Be(9.99m);
        (baseTotal + 1 * PaperBonus.Unit).Should().Be(ExamScoring.TotalScore);
    }

    // ---- kiểm request

    private static SaveExamRequest Request(int paperCount, int bonus) => new()
    {
        Title = "Kiểm tra giữa kỳ",
        DurationMinutes = 45,
        TotalQuestions = 9,
        EasyPercentage = 33,
        MediumPercentage = 33,
        HardPercentage = 34,
        Questions = [new ExamQuestionInput { QuestionId = 1, Point = 1.1m }],
        PaperCount = paperCount,
        PaperBonusUnits = bonus,
        OpenAt = OpenAt,
        CloseAt = OpenAt.AddHours(2)
    };

    private static IReadOnlyList<string> ErrorsOf(SaveExamRequest request) =>
        new SaveExamRequestValidator().Validate(request).Errors.Select(failure => failure.ErrorMessage).ToList();

    [Fact]
    public void De_chia_nhieu_de_nhan_diem_cong()
    {
        ErrorsOf(Request(paperCount: 4, bonus: 1)).Should().BeEmpty();
    }

    [Fact]
    public void De_mot_de_khong_duoc_co_diem_cong()
    {
        ErrorsOf(Request(paperCount: 1, bonus: 1)).Should().Contain(message => message.Contains("chỉ dùng được khi chia thành nhiều đề"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(SaveExamRequestValidator.MaxTotalQuestions + 1)]
    public void Diem_cong_ngoai_khoang_bi_tu_choi(int bonus)
    {
        ErrorsOf(Request(paperCount: 4, bonus: bonus)).Should().Contain(message => message.Contains("Điểm cộng thêm không hợp lệ"));
    }

    // ---- khoá cấu hình khi đã có em làm

    private static Exam PublishedExam() => new()
    {
        Title = "Kiểm tra giữa kỳ",
        DurationMinutes = 45,
        TotalQuestions = 9,
        EasyPercentage = 33,
        MediumPercentage = 33,
        HardPercentage = 34,
        PointEasy = 0.9m,
        PointMedium = 1.1m,
        PointHard = 1.33m,
        UsesQuestionList = true,
        PaperCount = 4,
        PaperBonusUnits = 1,
        ShuffleQuestions = true,
        ShuffleAnswers = true,
        ShowResultAfterSubmit = false,
        OpenAt = OpenAt,
        CloseAt = OpenAt.AddHours(2),
        IsDraft = false
    };

    private static SaveExamRequest SameAs(Exam exam) => new()
    {
        Title = exam.Title,
        DurationMinutes = exam.DurationMinutes,
        TotalQuestions = exam.TotalQuestions,
        EasyPercentage = exam.EasyPercentage,
        MediumPercentage = exam.MediumPercentage,
        HardPercentage = exam.HardPercentage,
        PointEasy = exam.PointEasy,
        PointMedium = exam.PointMedium,
        PointHard = exam.PointHard,
        Questions = [new ExamQuestionInput { QuestionId = 1, Point = 1.1m }],
        PaperCount = exam.PaperCount,
        PaperBonusUnits = exam.PaperBonusUnits,
        ShuffleQuestions = exam.ShuffleQuestions,
        ShuffleAnswers = exam.ShuffleAnswers,
        ShowResultAfterSubmit = exam.ShowResultAfterSubmit,
        OpenAt = exam.OpenAt,
        CloseAt = exam.CloseAt
    };

    [Fact]
    public void Gui_lai_nguyen_diem_cong_thi_khong_phai_sinh_lai_gi()
    {
        var exam = PublishedExam();

        ExamEditPolicy.Decide(exam, SameAs(exam), hasAssignments: true, hasStartedAttempts: false)
            .Decision.Should().Be(ExamEditDecision.Apply);
    }

    [Fact]
    public void Doi_diem_cong_khi_chua_ai_lam_thi_sinh_lai_ban_de()
    {
        var exam = PublishedExam();

        ExamEditPolicy.Decide(exam, SameAs(exam) with { PaperBonusUnits = 0 }, hasAssignments: true, hasStartedAttempts: false)
            .Decision.Should().Be(ExamEditDecision.ApplyAndRegenerate);
    }

    [Fact]
    public void Doi_diem_cong_khi_da_co_em_lam_thi_bi_khoa_va_goi_dung_ten_truong()
    {
        var exam = PublishedExam();

        var plan = ExamEditPolicy.Decide(exam, SameAs(exam) with { PaperBonusUnits = 2 }, hasAssignments: true, hasStartedAttempts: true);

        plan.Decision.Should().Be(ExamEditDecision.RejectFrozen);
        plan.FrozenFieldsChanged.Should().Contain("điểm cộng thêm của đề nhỏ");
    }
}
