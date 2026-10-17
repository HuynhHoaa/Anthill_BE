using Anthill.Application.Common;
using Anthill.Application.DTOs.Exams;
using Anthill.Domain.Entities;
using FluentAssertions;

namespace Anthill.Tests.Exams;

/// <summary>
/// Luật sửa đề đã xuất bản — chỗ quyết định sửa được gì, và bản đề đã sinh có phải sinh lại không.
/// </summary>
/// <remarks>
/// Hai lỗi đắt nhất ở đây đều im lặng. Để lọt một trường khoá thì nửa lớp làm đề cũ, nửa lớp làm đề
/// mới, và điểm của hai nửa vẫn bị đem so với nhau. Quên sinh lại khi cấu hình đổi thì giáo viên thấy
/// "20 câu" trên màn hình trong khi học sinh cầm bản đề 10 câu.
/// </remarks>
public class ExamEditPolicyTests
{
    private static readonly DateTime OpenAt = new(2026, 9, 20, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CloseAt = new(2026, 9, 21, 1, 0, 0, DateTimeKind.Utc);

    private static Exam PublishedExam() => new()
    {
        Title = "Kiểm tra 15 phút",
        Description = "Chương 1",
        Topic = "Điện học",
        DurationMinutes = 15,
        TotalQuestions = 10,
        EasyPercentage = 50,
        MediumPercentage = 30,
        HardPercentage = 20,
        PointEasy = 1m,
        PointMedium = 1m,
        PointHard = 1m,
        ShuffleQuestions = true,
        ShuffleAnswers = false,
        ShowResultAfterSubmit = true,
        OpenAt = OpenAt,
        CloseAt = CloseAt,
        IsDraft = false
    };

    /// <summary>Request gửi lại y nguyên cấu hình đang có — điểm xuất phát của mọi ca bên dưới.</summary>
    private static SaveExamRequest SameAs(Exam exam) => new()
    {
        Title = exam.Title,
        Description = exam.Description,
        Topic = exam.Topic,
        DurationMinutes = exam.DurationMinutes,
        TotalQuestions = exam.TotalQuestions,
        EasyPercentage = exam.EasyPercentage,
        MediumPercentage = exam.MediumPercentage,
        HardPercentage = exam.HardPercentage,
        PointEasy = exam.PointEasy,
        PointMedium = exam.PointMedium,
        PointHard = exam.PointHard,
        ShuffleQuestions = exam.ShuffleQuestions,
        ShuffleAnswers = exam.ShuffleAnswers,
        ShowResultAfterSubmit = exam.ShowResultAfterSubmit,
        OpenAt = exam.OpenAt,
        CloseAt = exam.CloseAt
    };

    /// <summary>Mỗi dòng đổi đúng một trường mà bộ sinh đề đọc.</summary>
    public static TheoryData<string, Func<SaveExamRequest, SaveExamRequest>> PaperFieldChanges => new()
    {
        { "số câu hỏi", r => r with { TotalQuestions = 20 } },
        { "điểm mỗi câu Dễ", r => r with { PointEasy = 0.5m } },
        { "điểm mỗi câu Trung bình", r => r with { PointMedium = 0.5m } },
        { "điểm mỗi câu Khó", r => r with { PointHard = 0.5m } },
        { "chủ đề", r => r with { Topic = "Quang học" } },
        { "tỷ lệ câu Dễ", r => r with { EasyPercentage = 40, HardPercentage = 30 } },
        { "tỷ lệ câu Trung bình", r => r with { MediumPercentage = 40, HardPercentage = 10 } },
        { "tỷ lệ câu Khó", r => r with { HardPercentage = 10, EasyPercentage = 60 } },
        { "trộn thứ tự câu hỏi", r => r with { ShuffleQuestions = false } },
        { "trộn thứ tự đáp án", r => r with { ShuffleAnswers = true } }
    };

    /// <summary>Mỗi dòng đổi đúng một trường không làm thay đổi bộ câu nào.</summary>
    public static TheoryData<string, Func<SaveExamRequest, SaveExamRequest>> FreeFieldChanges => new()
    {
        { "tên", r => r with { Title = "Kiểm tra 15 phút (đợt 2)" } },
        { "mô tả", r => r with { Description = "Chương 1 và 2" } },
        { "giờ mở", r => r with { OpenAt = OpenAt.AddHours(2) } },
        { "giờ đóng", r => r with { CloseAt = CloseAt.AddDays(1) } },
        { "hiện điểm", r => r with { ShowResultAfterSubmit = false } }
    };

    [Fact]
    public void Gui_lai_y_nguyen_thi_chi_ghi_khong_sinh_lai()
    {
        var exam = PublishedExam();

        ExamEditPolicy.Decide(exam, SameAs(exam), hasAssignments: true, hasStartedAttempts: false)
            .Should().Be(new ExamEditPlan(ExamEditDecision.Apply, []), because: "không có gì đổi thì bốc lại câu là vô cớ")
            .And.Match<ExamEditPlan>(plan => plan.FrozenFieldsChanged.Count == 0);
    }

    [Theory]
    [MemberData(nameof(PaperFieldChanges))]
    public void Chua_ai_lam_ma_doi_truong_bo_sinh_de_doc_thi_sinh_lai(
        string field,
        Func<SaveExamRequest, SaveExamRequest> change)
    {
        var exam = PublishedExam();

        ExamEditPolicy.Decide(exam, change(SameAs(exam)), hasAssignments: true, hasStartedAttempts: false)
            .Decision.Should().Be(ExamEditDecision.ApplyAndRegenerate, because: $"đổi {field} là đổi bộ câu");
    }

    [Theory]
    [MemberData(nameof(PaperFieldChanges))]
    public void Khong_con_luot_giao_nao_ma_doi_truong_bo_sinh_de_doc_thi_bo_ban_de_cu(
        string field,
        Func<SaveExamRequest, SaveExamRequest> change)
    {
        // Giao, thu hồi hết, rồi sửa: bản đề chung của lần giao cũ vẫn còn vì nó không gắn với lượt giao
        // nào. Để lại thì lần giao sau phát lại bộ câu của cấu hình cũ.
        var exam = PublishedExam();

        ExamEditPolicy.Decide(exam, change(SameAs(exam)), hasAssignments: false, hasStartedAttempts: false)
            .Decision.Should().Be(
                ExamEditDecision.ApplyAndDiscardPapers,
                because: $"đổi {field} thì bản đề cũ không còn khớp cấu hình");
    }

    [Theory]
    [MemberData(nameof(FreeFieldChanges))]
    public void Khong_con_luot_giao_nao_ma_chi_doi_truong_tu_do_thi_giu_ban_de(
        string field,
        Func<SaveExamRequest, SaveExamRequest> change)
    {
        var exam = PublishedExam();

        ExamEditPolicy.Decide(exam, change(SameAs(exam)), hasAssignments: false, hasStartedAttempts: false)
            .Decision.Should().Be(ExamEditDecision.Apply, because: $"{field} không đổi bộ câu nào");
    }

    [Fact]
    public void Ban_nhap_khong_con_luot_giao_van_bo_ban_de_cu_khi_doi_cau_hinh()
    {
        // Rút về nháp sau khi thu hồi hết rồi sửa dở cấu hình: bỏ bản đề không cần ngân hàng đủ câu, nên nút
        // "Lưu nháp" vẫn dùng được, mà lần giao sau (sau khi xuất bản) vẫn sinh theo cấu hình mới.
        var exam = PublishedExam();
        exam.IsDraft = true;

        ExamEditPolicy.Decide(
                exam,
                SameAs(exam) with { Lifecycle = "draft", TotalQuestions = 25 },
                hasAssignments: false,
                hasStartedAttempts: false)
            .Decision.Should().Be(ExamEditDecision.ApplyAndDiscardPapers);
    }

    [Theory]
    [MemberData(nameof(FreeFieldChanges))]
    public void Chua_ai_lam_ma_chi_doi_truong_tu_do_thi_khong_sinh_lai(
        string field,
        Func<SaveExamRequest, SaveExamRequest> change)
    {
        var exam = PublishedExam();

        ExamEditPolicy.Decide(exam, change(SameAs(exam)), hasAssignments: true, hasStartedAttempts: false)
            .Decision.Should().Be(ExamEditDecision.Apply, because: $"{field} không đổi bộ câu nào");
    }

    [Fact]
    public void Chua_ai_lam_ma_doi_thoi_luong_thi_khong_sinh_lai()
    {
        // Bộ sinh đề không đọc thời lượng: hạn nộp tính lúc em bắt đầu làm, từ chính đề.
        var exam = PublishedExam();

        ExamEditPolicy.Decide(exam, SameAs(exam) with { DurationMinutes = 45 }, hasAssignments: true, hasStartedAttempts: false)
            .Decision.Should().Be(ExamEditDecision.Apply);
    }

    [Theory]
    [MemberData(nameof(PaperFieldChanges))]
    public void Da_co_em_lam_thi_truong_bo_sinh_de_doc_bi_khoa(
        string field,
        Func<SaveExamRequest, SaveExamRequest> change)
    {
        var exam = PublishedExam();

        var plan = ExamEditPolicy.Decide(exam, change(SameAs(exam)), hasAssignments: true, hasStartedAttempts: true);

        plan.Decision.Should().Be(ExamEditDecision.RejectFrozen);
        plan.FrozenFieldsChanged.Should().Contain(field);
    }

    [Fact]
    public void Da_co_em_lam_thi_thoi_luong_cung_bi_khoa()
    {
        // Em đã vào thi có hạn nộp chốt theo thời lượng cũ; em vào sau mà nhận thời lượng mới là cùng
        // một đề, hai quỹ thời gian.
        var exam = PublishedExam();

        var plan = ExamEditPolicy.Decide(
            exam, SameAs(exam) with { DurationMinutes = 45 }, hasAssignments: true, hasStartedAttempts: true);

        plan.Decision.Should().Be(ExamEditDecision.RejectFrozen);
        plan.FrozenFieldsChanged.Should().Equal("thời gian làm bài");
    }

    [Theory]
    [MemberData(nameof(FreeFieldChanges))]
    public void Da_co_em_lam_van_sua_duoc_truong_tu_do(
        string field,
        Func<SaveExamRequest, SaveExamRequest> change)
    {
        var exam = PublishedExam();

        ExamEditPolicy.Decide(exam, change(SameAs(exam)), hasAssignments: true, hasStartedAttempts: true)
            .Should().Be(new ExamEditPlan(ExamEditDecision.Apply, []), because: $"{field} vẫn phải sửa được")
            .And.Match<ExamEditPlan>(plan => plan.FrozenFieldsChanged.Count == 0);
    }

    [Fact]
    public void Doi_nhieu_truong_khoa_thi_ke_du_theo_thu_tu_tren_form()
    {
        var exam = PublishedExam();
        var request = SameAs(exam) with
        {
            ShuffleAnswers = true,
            TotalQuestions = 12,
            PointMedium = 2m
        };

        ExamEditPolicy.Decide(exam, request, hasAssignments: true, hasStartedAttempts: true)
            .FrozenFieldsChanged.Should().Equal("số câu hỏi", "điểm mỗi câu Trung bình", "trộn thứ tự đáp án");
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", null)]
    [InlineData(null, "all")]
    [InlineData("", "  ")]
    public void Ba_cach_viet_mo_moi_chu_de_la_cung_mot_chu_de(string? stored, string? requested)
    {
        // Frontend gửi chuỗi rỗng, database lưu NULL. So thẳng hai chuỗi thì một đề đã có em làm sẽ bị
        // từ chối vì "đổi chủ đề" trong khi giáo viên không chạm vào ô chủ đề.
        var exam = PublishedExam();
        exam.Topic = stored;

        ExamEditPolicy.Decide(exam, SameAs(exam) with { Topic = requested }, hasAssignments: true, hasStartedAttempts: true)
            .Decision.Should().Be(ExamEditDecision.Apply);
    }

    [Fact]
    public void Da_co_em_lam_thi_khong_rut_ve_nhap_duoc()
    {
        var exam = PublishedExam();

        ExamEditPolicy.Decide(exam, SameAs(exam) with { Lifecycle = "draft" }, hasAssignments: true, hasStartedAttempts: true)
            .Decision.Should().Be(ExamEditDecision.RejectBackToDraft);
    }

    [Fact]
    public void Chua_ai_lam_thi_van_rut_ve_nhap_duoc()
    {
        var exam = PublishedExam();

        ExamEditPolicy.Decide(exam, SameAs(exam) with { Lifecycle = "draft" }, hasAssignments: true, hasStartedAttempts: false)
            .Decision.Should().Be(ExamEditDecision.Apply);
    }

    [Fact]
    public void Rut_ve_nhap_duoc_bao_truoc_ca_truong_khoa()
    {
        // Hai lỗi cùng lúc thì báo lỗi không sửa được bằng cách trả ô về giá trị cũ: nhân bản đề không
        // phải lối ra cho việc rút về nháp.
        var exam = PublishedExam();

        ExamEditPolicy.Decide(
                exam,
                SameAs(exam) with { Lifecycle = "draft", TotalQuestions = 30 },
                hasAssignments: true,
                hasStartedAttempts: true)
            .Decision.Should().Be(ExamEditDecision.RejectBackToDraft);
    }

    // ── H9: sinh lại bản đề khi một câu hỏi trong ngân hàng đổi ───────────────────────────────────
    //
    // QuestionService gọi DecidePaperRefresh cho MỖI đề mà bản đề đã sinh của nó đang chứa câu hỏi vừa
    // sửa/ẩn. Cùng ranh giới "lượt làm bài đầu tiên" mà Decide() dùng cho lần sửa cả đề — pin lại ở đây
    // vì đây là đúng chỗ H9 (đáp án đã sửa nhưng bản đề cũ không cập nhật) có thể tái diễn nếu ai đó nới
    // lỏng ranh giới này sau này.

    [Fact]
    public void Da_co_em_bat_dau_thi_bo_qua_du_con_luot_giao()
    {
        // Đã khoá cấu hình theo lượt làm bài đầu tiên (giống Decide()): không đụng bản đề đang dùng, dù
        // đề vẫn còn lượt giao chưa bắt đầu khác — nửa đề cũ nửa đề mới còn tệ hơn giữ nguyên cả đề cũ.
        ExamEditPolicy.DecidePaperRefresh(hasAssignments: true, hasStartedAttempts: true)
            .Should().Be(ExamEditPolicy.PaperRefreshDecision.Skip);
    }

    [Fact]
    public void Chua_ai_bat_dau_va_con_luot_giao_thi_sinh_lai()
    {
        ExamEditPolicy.DecidePaperRefresh(hasAssignments: true, hasStartedAttempts: false)
            .Should().Be(ExamEditPolicy.PaperRefreshDecision.Regenerate);
    }

    [Fact]
    public void Khong_con_luot_giao_nao_thi_bo_ban_de_cu_khong_sinh_lai()
    {
        // Thu hồi hết rồi mới sửa câu hỏi: không còn ai để sinh đề cho ngay bây giờ, nhưng bản đề chung
        // cũ (không gắn với lượt giao nào) vẫn phải bị xoá — để lại thì lần giao sau phát lại bản cũ.
        ExamEditPolicy.DecidePaperRefresh(hasAssignments: false, hasStartedAttempts: false)
            .Should().Be(ExamEditPolicy.PaperRefreshDecision.DiscardPapers);
    }

    [Fact]
    public void To_hop_khong_the_xay_ra_van_nghieng_ve_an_toan()
    {
        // Cả hai lệnh gọi thật (ExamService.UpdateAsync và QuestionService) đều suy hasStartedAttempts
        // từ CHÍNH danh sách assignments vừa khoá — assignments rỗng thì luôn chốt hasStartedAttempts =
        // false trước khi tới đây, nên tổ hợp (false, true) không bao giờ xảy ra qua hai đường đó. Đây
        // là một hàm thuần nên vẫn pin nốt góc còn lại của bảng chân trị: SKIP (không đụng gì) an toàn
        // hơn DiscardPapers (xoá bản đề) nếu một lời gọi tương lai nào đó tính hai cờ độc lập với nhau.
        ExamEditPolicy.DecidePaperRefresh(hasAssignments: false, hasStartedAttempts: true)
            .Should().Be(ExamEditPolicy.PaperRefreshDecision.Skip);
    }
}
