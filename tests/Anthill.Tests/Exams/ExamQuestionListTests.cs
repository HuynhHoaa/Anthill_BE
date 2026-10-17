using Anthill.Application.Common;
using Anthill.Application.DTOs.Exams;
using Anthill.Application.Validators;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Exams;

/// <summary>
/// Đề soạn theo danh sách câu hỏi: số liệu ma trận dẫn xuất, so sánh danh sách, luật sửa đề, tổng điểm và
/// hình dạng request.
/// </summary>
/// <remarks>
/// Cái giá của việc vẫn ghi các cột ma trận cho một đề theo danh sách là phải tin chúng nhất quán với danh
/// sách. Các ca ở đây khoá ba chỗ dễ lệch: ba tỷ lệ phải cộng đúng 100, tổng điểm không được suy ngược từ
/// điểm trung bình, và một lần thay câu cùng mức phải bị nhận ra là "danh sách đổi".
/// </remarks>
public class ExamQuestionListTests
{
    private static readonly DateTime OpenAt = new(2026, 9, 20, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CloseAt = new(2026, 9, 21, 1, 0, 0, DateTimeKind.Utc);

    private const QuestionDifficulty Easy = QuestionDifficulty.Easy;
    private const QuestionDifficulty Medium = QuestionDifficulty.Medium;
    private const QuestionDifficulty Hard = QuestionDifficulty.Hard;

    // ------------------------------------------------------------------ Derive

    [Fact]
    public void Dan_xuat_dem_dung_so_cau_va_tong_diem_tung_cau()
    {
        var derived = ExamQuestionList.Derive(
        [
            (Easy, 4m), (Easy, 4m), (Medium, 5m), (Medium, 6m), (Hard, 10m)
        ]);

        derived.TotalQuestions.Should().Be(5);
        derived.TotalScore.Should().Be(29m);
    }

    [Fact]
    public void Ba_ty_le_luon_cong_dung_100()
    {
        // 7 câu chia 3/3/1: 42,86 / 42,86 / 14,29 → làm tròn xuống 42/42/14 còn dư 2, chia cho hai phần thập phân
        // lớn nhất. Ba số nguyên độc lập (43/43/14) sẽ cộng ra 100 hay 101 tuỳ may rủi; ở đây phải luôn đúng.
        var derived = ExamQuestionList.Derive(
        [
            (Easy, 1m), (Easy, 1m), (Easy, 1m),
            (Medium, 1m), (Medium, 1m), (Medium, 1m),
            (Hard, 1m)
        ]);

        (derived.EasyPercentage + derived.MediumPercentage + derived.HardPercentage).Should().Be(100);
        derived.EasyPercentage.Should().Be(43);
        derived.MediumPercentage.Should().Be(43);
        derived.HardPercentage.Should().Be(14);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(5, 7, 11)]
    [InlineData(13, 29, 3)]
    [InlineData(0, 0, 9)]
    public void Ty_le_cong_dung_100_voi_moi_to_hop_so_cau(int easy, int medium, int hard)
    {
        var items = Enumerable.Repeat((Easy, 1m), easy)
            .Concat(Enumerable.Repeat((Medium, 1m), medium))
            .Concat(Enumerable.Repeat((Hard, 1m), hard))
            .ToList();

        var derived = ExamQuestionList.Derive(items);

        (derived.EasyPercentage + derived.MediumPercentage + derived.HardPercentage).Should().Be(100);
        derived.TotalQuestions.Should().Be(easy + medium + hard);
    }

    [Fact]
    public void Danh_sach_rong_cho_ba_so_khong()
    {
        var derived = ExamQuestionList.Derive([]);

        derived.TotalQuestions.Should().Be(0);
        derived.EasyPercentage.Should().Be(0);
        derived.MediumPercentage.Should().Be(0);
        derived.HardPercentage.Should().Be(0);
        derived.TotalScore.Should().Be(0m);
    }

    [Fact]
    public void Diem_theo_muc_la_trung_binh_nhung_tong_diem_la_tong_that()
    {
        // Ba câu Dễ 1, 1 và 5 điểm: trung bình 2,33 (làm tròn), nhưng tổng thật là 7. Dựng lại tổng từ
        // "3 câu × 2,33" ra 6,99 — đó là lý do tổng điểm phải được chụp, không suy ngược.
        var derived = ExamQuestionList.Derive([(Easy, 1m), (Easy, 1m), (Easy, 5m)]);

        derived.PointEasy.Should().Be(2.33m);
        derived.TotalScore.Should().Be(7m);
        (derived.PointEasy * derived.TotalQuestions).Should().NotBe(derived.TotalScore);
    }

    [Fact]
    public void Muc_khong_co_cau_nao_co_diem_trung_binh_bang_khong()
    {
        var derived = ExamQuestionList.Derive([(Easy, 2m)]);

        derived.PointMedium.Should().Be(0m);
        derived.PointHard.Should().Be(0m);
    }

    // ------------------------------------------------------------------ Differs

    private static List<ExamQuestion> Rows(params (int QuestionId, decimal Point)[] rows) =>
        rows.Select((row, index) => new ExamQuestion
        {
            QuestionId = row.QuestionId,
            Ordinal = index,
            Point = row.Point
        }).ToList();

    private static List<ExamQuestionInput> Inputs(params (int QuestionId, decimal Point)[] rows) =>
        rows.Select(row => new ExamQuestionInput { QuestionId = row.QuestionId, Point = row.Point }).ToList();

    [Fact]
    public void Danh_sach_y_nguyen_thi_khong_doi()
    {
        ExamQuestionList.Differs(Rows((1, 2m), (2, 3m)), Inputs((1, 2m), (2, 3m))).Should().BeFalse();
    }

    [Fact]
    public void Thay_mot_cau_bang_cau_khac_cung_diem_la_doi()
    {
        // Số câu, tỷ lệ và tổng điểm đều y nguyên nếu hai câu cùng mức — chỉ danh sách mới thấy khác.
        ExamQuestionList.Differs(Rows((1, 2m), (2, 3m)), Inputs((1, 2m), (9, 3m))).Should().BeTrue();
    }

    [Fact]
    public void Doi_thu_tu_la_doi()
    {
        ExamQuestionList.Differs(Rows((1, 2m), (2, 3m)), Inputs((2, 3m), (1, 2m))).Should().BeTrue();
    }

    [Fact]
    public void Doi_diem_mot_cau_la_doi()
    {
        ExamQuestionList.Differs(Rows((1, 2m), (2, 3m)), Inputs((1, 2.5m), (2, 3m))).Should().BeTrue();
    }

    [Fact]
    public void Them_hoac_bot_cau_la_doi()
    {
        ExamQuestionList.Differs(Rows((1, 2m)), Inputs((1, 2m), (2, 3m))).Should().BeTrue();
        ExamQuestionList.Differs(Rows((1, 2m), (2, 3m)), Inputs((1, 2m))).Should().BeTrue();
    }

    [Fact]
    public void So_sanh_theo_thu_tu_da_luu_chu_khong_theo_thu_tu_nap_ve()
    {
        // Database có thể trả các dòng không theo Ordinal; thứ tự giáo viên thấy là Ordinal.
        var shuffled = Rows((1, 2m), (2, 3m));
        shuffled.Reverse();

        ExamQuestionList.Differs(shuffled, Inputs((1, 2m), (2, 3m))).Should().BeFalse();
    }

    // ------------------------------------------------------------------ tổng điểm

    [Fact]
    public void De_theo_danh_sach_lay_tong_diem_da_chup_chu_khong_tinh_lai_tu_ma_tran()
    {
        var exam = new Exam
        {
            Title = "x",
            TotalQuestions = 3,
            EasyPercentage = 100,
            PointEasy = 2m,
            UsesQuestionList = true,
            ListTotalScore = 6.01m
        };

        // Ma trận nói 3 × 2 = 6; danh sách nói 6,01. Phải đọc danh sách.
        ExamScoring.TotalOf(exam).Should().Be(6.01m);
    }

    [Fact]
    public void De_theo_ma_tran_van_tinh_nhu_cu()
    {
        var exam = new Exam
        {
            Title = "x",
            TotalQuestions = 10,
            EasyPercentage = 100,
            MediumPercentage = 0,
            HardPercentage = 0,
            PointEasy = 2m,
            ListTotalScore = 999m
        };

        ExamScoring.TotalOf(exam).Should().Be(20m);
    }

    [Fact]
    public void Tong_diem_cua_request_theo_danh_sach_la_tong_tung_cau()
    {
        var request = Request(Inputs((1, 10m), (2, 20.5m), (3, 4.25m)));

        ExamScoring.TotalOf(request).Should().Be(34.75m);
    }

    // ------------------------------------------------------------------ luật sửa đề

    private static Exam ListExam() => new()
    {
        Title = "Kiểm tra 15 phút",
        Description = "Chương 1",
        Topic = null,
        DurationMinutes = 15,
        TotalQuestions = 2,
        EasyPercentage = 50,
        MediumPercentage = 50,
        HardPercentage = 0,
        PointEasy = 4m,
        PointMedium = 6m,
        PointHard = 0m,
        UsesQuestionList = true,
        ListTotalScore = 10m,
        ShuffleQuestions = true,
        ShuffleAnswers = false,
        ShowResultAfterSubmit = true,
        OpenAt = OpenAt,
        CloseAt = CloseAt,
        IsDraft = false
    };

    private static SaveExamRequest Request(IReadOnlyList<ExamQuestionInput> questions, Exam? basedOn = null) => new()
    {
        Title = basedOn?.Title ?? "Kiểm tra 15 phút",
        Description = basedOn?.Description ?? "Chương 1",
        DurationMinutes = basedOn?.DurationMinutes ?? 15,
        ShuffleQuestions = basedOn?.ShuffleQuestions ?? true,
        ShuffleAnswers = basedOn?.ShuffleAnswers ?? false,
        ShowResultAfterSubmit = basedOn?.ShowResultAfterSubmit ?? true,
        OpenAt = OpenAt,
        CloseAt = CloseAt,
        Questions = questions
    };

    [Fact]
    public void Danh_sach_y_nguyen_thi_chi_ghi_khong_sinh_lai_ban_de()
    {
        var exam = ListExam();

        var plan = ExamEditPolicy.Decide(
            exam, Request(Inputs((1, 4m), (2, 6m)), exam),
            hasAssignments: true, hasStartedAttempts: false, questionListChanged: false);

        plan.Decision.Should().Be(ExamEditDecision.Apply);
    }

    [Fact]
    public void Danh_sach_doi_va_da_giao_thi_sinh_lai_ban_de()
    {
        var exam = ListExam();

        var plan = ExamEditPolicy.Decide(
            exam, Request(Inputs((1, 4m), (3, 6m)), exam),
            hasAssignments: true, hasStartedAttempts: false, questionListChanged: true);

        plan.Decision.Should().Be(ExamEditDecision.ApplyAndRegenerate);
    }

    [Fact]
    public void Danh_sach_doi_ma_chua_giao_thi_bo_ban_de_cu()
    {
        var exam = ListExam();

        var plan = ExamEditPolicy.Decide(
            exam, Request(Inputs((1, 4m), (3, 6m)), exam),
            hasAssignments: false, hasStartedAttempts: false, questionListChanged: true);

        plan.Decision.Should().Be(ExamEditDecision.ApplyAndDiscardPapers);
    }

    [Fact]
    public void Co_em_bat_dau_lam_thi_danh_sach_bi_khoa_va_bao_dung_ten_truong()
    {
        var exam = ListExam();

        var plan = ExamEditPolicy.Decide(
            exam, Request(Inputs((1, 4m), (3, 6m)), exam),
            hasAssignments: true, hasStartedAttempts: true, questionListChanged: true);

        plan.Decision.Should().Be(ExamEditDecision.RejectFrozen);
        // Chỉ một tên trường mà form có, không phải ba cột ma trận dẫn xuất mà giáo viên chưa từng nhập.
        plan.FrozenFieldsChanged.Should().Equal("danh sách câu hỏi và điểm từng câu");
    }

    [Fact]
    public void Co_em_bat_dau_lam_thi_doi_ten_va_mo_ta_van_luu_duoc()
    {
        var exam = ListExam();
        var request = Request(Inputs((1, 4m), (2, 6m)), exam) with { Title = "Tên mới", Description = "Mô tả mới" };

        var plan = ExamEditPolicy.Decide(
            exam, request, hasAssignments: true, hasStartedAttempts: true, questionListChanged: false);

        plan.Decision.Should().Be(ExamEditDecision.Apply);
    }

    [Fact]
    public void Cac_cot_ma_tran_trong_request_theo_danh_sach_khong_bi_coi_la_doi()
    {
        // Client gửi ma trận bằng không (không có gì để gửi). Với đề theo danh sách đó không phải "đổi số câu
        // thành 0" — server dựng lại các cột này từ danh sách.
        var exam = ListExam();
        var request = Request(Inputs((1, 4m), (2, 6m)), exam) with
        {
            TotalQuestions = 0, EasyPercentage = 0, MediumPercentage = 0, HardPercentage = 0,
            PointEasy = 0m, PointMedium = 0m, PointHard = 0m, Topic = "Quang học"
        };

        ExamEditPolicy.ChangedFrozenFields(exam, request, questionListChanged: false).Should().BeEmpty();
        ExamEditPolicy.ChangesGeneratedPaper(exam, request, questionListChanged: false).Should().BeFalse();
    }

    [Fact]
    public void Doi_tuy_chon_tron_van_sinh_lai_ban_de_cua_de_theo_danh_sach()
    {
        var exam = ListExam();
        var request = Request(Inputs((1, 4m), (2, 6m)), exam) with { ShuffleAnswers = true };

        ExamEditPolicy.ChangesGeneratedPaper(exam, request, questionListChanged: false).Should().BeTrue();
    }

    // ------------------------------------------------------------------ hình dạng request

    private readonly SaveExamRequestValidator _validator = new();

    private IReadOnlyList<string> ErrorsOf(SaveExamRequest request) =>
        _validator.Validate(request).Errors.Select(failure => failure.ErrorMessage).ToList();

    private static SaveExamRequest ListRequest(IReadOnlyList<ExamQuestionInput> questions) => new()
    {
        Title = "Kiểm tra giữa kỳ",
        DurationMinutes = 45,
        OpenAt = OpenAt,
        CloseAt = OpenAt.AddHours(2),
        Questions = questions
    };

    [Fact]
    public void De_theo_danh_sach_khong_can_gui_cac_truong_ma_tran()
    {
        // Không TotalQuestions, không tỷ lệ, không điểm theo mức — vẫn hợp lệ.
        _validator.Validate(ListRequest(Inputs((1, 5m), (2, 5m)))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Nhap_cho_phep_danh_sach_rong()
    {
        _validator.Validate(ListRequest([])).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Muc_rong_trong_danh_sach_bi_tu_choi_chu_khong_ne_ngoai_le()
    {
        // System.Text.Json cho "questions": [null] lọt vào DTO. Mọi rule đọc .QuestionId / .Point trên phần tử đó
        // từng ném NullReferenceException, tức 500 thay vì 400.
        var request = ListRequest([new ExamQuestionInput { QuestionId = 1, Point = 5m }, null!]);

        var act = () => _validator.Validate(request);

        act.Should().NotThrow();
        ErrorsOf(request).Should().Contain("Danh sách câu hỏi chứa một mục rỗng.");
    }

    [Fact]
    public void De_theo_ma_tran_van_bi_bat_so_cau()
    {
        var request = new SaveExamRequest
        {
            Title = "x",
            DurationMinutes = 45,
            OpenAt = OpenAt,
            CloseAt = OpenAt.AddHours(2)
        };

        ErrorsOf(request).Should().Contain(message => message.Contains("Số câu hỏi của đề phải từ"));
    }

    [Fact]
    public void Mot_cau_hoi_khong_xuat_hien_hai_lan()
    {
        ErrorsOf(ListRequest(Inputs((1, 5m), (1, 5m))))
            .Should().Contain("Một câu hỏi chỉ được xuất hiện một lần trong đề.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    [InlineData(2.555)]
    public void Diem_mot_cau_phai_trong_tran_va_toi_da_hai_chu_so_thap_phan(double point)
    {
        ErrorsOf(ListRequest(Inputs((1, (decimal)point)))).Should().NotBeEmpty();
    }

    [Fact]
    public void Tong_diem_khong_duoc_vuot_cot_diem_toi_da_cua_luot_lam_bai()
    {
        // 101 câu × 100 điểm = 10100 > 9999,99: tràn numeric(6,2) ngay khi học sinh đầu tiên bắt đầu làm.
        var items = Enumerable.Range(1, 101).Select(id => (id, 100m)).ToArray();

        ErrorsOf(ListRequest(Inputs(items)))
            .Should().Contain(message => message.Contains("Tổng điểm bài kiểm tra không được vượt quá"));
    }

    [Fact]
    public void Diem_tran_cot_khong_gay_loi_cong_don()
    {
        // Điểm cỡ decimal.MaxValue: rule riêng báo lỗi, và phép cộng tổng không được ném OverflowException.
        var request = ListRequest(
        [
            new ExamQuestionInput { QuestionId = 1, Point = decimal.MaxValue },
            new ExamQuestionInput { QuestionId = 2, Point = decimal.MaxValue }
        ]);

        var act = () => _validator.Validate(request);

        act.Should().NotThrow();
        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Ma_cau_hoi_phai_duong()
    {
        ErrorsOf(ListRequest(Inputs((0, 5m)))).Should().Contain("Mã câu hỏi trong danh sách không hợp lệ.");
    }

    // ------------------------------------------------------------------ soạn tự động

    private readonly ComposeQuestionsRequestValidator _composeValidator = new();

    [Fact]
    public void Soan_tu_dong_can_it_nhat_mot_cau()
    {
        _composeValidator.Validate(new ComposeQuestionsRequest()).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Soan_tu_dong_hop_le_khi_co_so_cau_o_mot_muc()
    {
        _composeValidator.Validate(new ComposeQuestionsRequest { Hard = 3 }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Soan_tu_dong_khong_nhan_so_am_hoac_vuot_tran()
    {
        _composeValidator.Validate(new ComposeQuestionsRequest { Easy = -1, Medium = 2 }).IsValid.Should().BeFalse();
        _composeValidator.Validate(new ComposeQuestionsRequest { Easy = 300, Medium = 300 }).IsValid.Should().BeFalse();
    }
}
