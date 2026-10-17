using Anthill.Application.Common;
using Anthill.Application.DTOs.Exams;
using Anthill.Application.Validators;
using Anthill.Domain.Entities;
using FluentAssertions;

namespace Anthill.Tests.Exams;

/// <summary>Luật của đề được chia thành nhiều đề nhỏ: kiểm request, khoá cấu hình, và nhãn đề.</summary>
public class MultiPaperRulesTests
{
    private static readonly DateTime OpenAt = new(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyList<ExamQuestionInput> OneQuestion =
    [
        new ExamQuestionInput { QuestionId = 1, Point = 0.33m }
    ];

    /// <summary>Request đề theo danh sách chia thành <paramref name="paperCount"/> đề, mỗi đề 30 câu 10/10/10.</summary>
    private static SaveExamRequest MultiPaper(int paperCount = 4, int perPaper = 30, IReadOnlyList<ExamQuestionInput>? questions = null) => new()
    {
        Title = "Kiểm tra giữa kỳ",
        DurationMinutes = 45,
        TotalQuestions = perPaper,
        EasyPercentage = 33,
        MediumPercentage = 33,
        HardPercentage = 34,
        Questions = questions ?? OneQuestion,
        PaperCount = paperCount,
        OpenAt = OpenAt,
        CloseAt = OpenAt.AddHours(2)
    };

    private static IReadOnlyList<string> ErrorsOf(SaveExamRequest request) =>
        new SaveExamRequestValidator().Validate(request).Errors.Select(failure => failure.ErrorMessage).ToList();

    [Fact]
    public void De_chia_4_de_theo_danh_sach_hop_le()
    {
        ErrorsOf(MultiPaper()).Should().BeEmpty();
    }

    [Fact]
    public void Mac_dinh_la_mot_de_va_van_hop_le_voi_danh_sach_khong_gui_cau_hinh_mot_de()
    {
        var request = new SaveExamRequest
        {
            Title = "Đề thường",
            DurationMinutes = 45,
            Questions = OneQuestion,
            OpenAt = OpenAt,
            CloseAt = OpenAt.AddHours(2)
        };

        request.PaperCount.Should().Be(1);
        ErrorsOf(request).Should().BeEmpty("đề theo danh sách một đề không phải gửi số câu và tỷ lệ");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(PaperPlanner.MaxPaperCount + 1)]
    public void So_de_ngoai_khoang_bi_tu_choi(int paperCount)
    {
        ErrorsOf(MultiPaper(paperCount)).Should().Contain(message => message.Contains("Số đề phải từ 1 đến"));
    }

    [Fact]
    public void Chia_nhieu_de_ma_khong_co_danh_sach_cau_hoi_bi_tu_choi()
    {
        var request = MultiPaper() with { Questions = null, PointEasy = 0.5m, PointMedium = 0.5m, PointHard = 0.5m };

        ErrorsOf(request).Should().Contain(message => message.Contains("chỉ dùng được với đề soạn theo danh sách"));
    }

    [Fact]
    public void De_chia_nhieu_de_phai_co_so_cau_moi_de_hop_le()
    {
        ErrorsOf(MultiPaper(perPaper: 0)).Should().Contain(message => message.Contains("Số câu hỏi của đề phải từ 1 đến"));
    }

    [Fact]
    public void Ty_le_ngoai_khoang_cua_de_nho_bi_tu_choi()
    {
        var request = MultiPaper() with { EasyPercentage = 120 };

        ErrorsOf(request).Should().Contain(message => message.Contains("Tỷ lệ câu Dễ"));
    }

    // ---- khoá cấu hình khi đã có em làm bài

    private static Exam PublishedMultiPaperExam() => new()
    {
        Title = "Kiểm tra giữa kỳ",
        DurationMinutes = 45,
        TotalQuestions = 30,
        EasyPercentage = 33,
        MediumPercentage = 33,
        HardPercentage = 34,
        PointEasy = 0.33m,
        PointMedium = 0.33m,
        PointHard = 0.33m,
        UsesQuestionList = true,
        PaperCount = 4,
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
        Questions = OneQuestion,
        PaperCount = exam.PaperCount,
        ShuffleQuestions = exam.ShuffleQuestions,
        ShuffleAnswers = exam.ShuffleAnswers,
        ShowResultAfterSubmit = exam.ShowResultAfterSubmit,
        OpenAt = exam.OpenAt,
        CloseAt = exam.CloseAt
    };

    [Fact]
    public void Gui_lai_y_nguyen_cau_hinh_de_chia_nhieu_de_thi_khong_phai_sinh_lai_gi()
    {
        var exam = PublishedMultiPaperExam();

        var plan = ExamEditPolicy.Decide(exam, SameAs(exam), hasAssignments: true, hasStartedAttempts: false);

        plan.Decision.Should().Be(ExamEditDecision.Apply);
    }

    [Fact]
    public void Doi_so_de_khi_chua_ai_lam_thi_chia_va_phat_lai()
    {
        var exam = PublishedMultiPaperExam();

        var plan = ExamEditPolicy.Decide(exam, SameAs(exam) with { PaperCount = 3 }, hasAssignments: true, hasStartedAttempts: false);

        plan.Decision.Should().Be(ExamEditDecision.ApplyAndRegenerate);
    }

    [Fact]
    public void Doi_so_cau_moi_de_hay_ty_le_khi_chua_ai_lam_thi_sinh_lai()
    {
        var exam = PublishedMultiPaperExam();

        ExamEditPolicy.Decide(exam, SameAs(exam) with { TotalQuestions = 20 }, true, false)
            .Decision.Should().Be(ExamEditDecision.ApplyAndRegenerate);
        ExamEditPolicy.Decide(exam, SameAs(exam) with { EasyPercentage = 40, MediumPercentage = 40, HardPercentage = 20 }, true, false)
            .Decision.Should().Be(ExamEditDecision.ApplyAndRegenerate);
    }

    [Fact]
    public void Doi_so_de_khi_da_co_em_lam_bai_thi_bi_khoa_va_goi_dung_ten_truong()
    {
        var exam = PublishedMultiPaperExam();

        var plan = ExamEditPolicy.Decide(exam, SameAs(exam) with { PaperCount = 2 }, hasAssignments: true, hasStartedAttempts: true);

        plan.Decision.Should().Be(ExamEditDecision.RejectFrozen);
        plan.FrozenFieldsChanged.Should().Contain("số đề");
    }

    [Fact]
    public void Doi_so_cau_moi_de_khi_da_co_em_lam_bai_thi_bi_khoa()
    {
        var exam = PublishedMultiPaperExam();

        var plan = ExamEditPolicy.Decide(exam, SameAs(exam) with { TotalQuestions = 20 }, true, true);

        plan.Decision.Should().Be(ExamEditDecision.RejectFrozen);
        plan.FrozenFieldsChanged.Should().Contain("số câu hỏi");
    }

    [Fact]
    public void Sua_ten_khi_da_co_em_lam_bai_van_duoc()
    {
        var exam = PublishedMultiPaperExam();

        var plan = ExamEditPolicy.Decide(exam, SameAs(exam) with { Title = "Tên khác" }, true, true);

        plan.Decision.Should().Be(ExamEditDecision.Apply);
    }

    [Fact]
    public void Chuyen_de_mot_ban_chung_thanh_nhieu_de_la_doi_cach_sinh_de()
    {
        var single = PublishedMultiPaperExam();
        single.PaperCount = 1;

        var plan = ExamEditPolicy.Decide(single, SameAs(single) with { PaperCount = 4 }, hasAssignments: true, hasStartedAttempts: false);

        plan.Decision.Should().Be(ExamEditDecision.ApplyAndRegenerate);
    }

    // ---- nhãn đề

    [Theory]
    [InlineData(0, "A")]
    [InlineData(1, "B")]
    [InlineData(3, "D")]
    [InlineData(25, "Z")]
    [InlineData(26, "27")]
    public void Ma_de_tu_so_thu_tu(int index, string expected)
    {
        PaperLabels.Of(index).Should().Be(expected);
    }

    [Fact]
    public void De_chia_nhieu_de_dung_ban_de_rieng_cho_tung_em()
    {
        var exam = PublishedMultiPaperExam();

        exam.UsesPerAssignmentPapers.Should().BeTrue();
        exam.RandomizePerStudent.Should().BeFalse("cờ riêng cho đề ma trận vẫn tắt");

        exam.PaperCount = 1;
        exam.UsesPerAssignmentPapers.Should().BeFalse();

        exam.RandomizePerStudent = true;
        exam.UsesPerAssignmentPapers.Should().BeTrue();
    }
}
