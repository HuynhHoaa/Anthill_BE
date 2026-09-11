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
        PointPerQuestion = 1m,
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
        PointPerQuestion = exam.PointPerQuestion,
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
        { "điểm mỗi câu", r => r with { PointPerQuestion = 0.5m } },
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
    public void Chua_giao_cho_ai_thi_khong_co_ban_de_nao_de_sinh_lai(
        string field,
        Func<SaveExamRequest, SaveExamRequest> change)
    {
        var exam = PublishedExam();

        ExamEditPolicy.Decide(exam, change(SameAs(exam)), hasAssignments: false, hasStartedAttempts: false)
            .Decision.Should().Be(ExamEditDecision.Apply, because: $"đổi {field} chỉ áp ở lần giao đầu tiên");
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
            PointPerQuestion = 2m
        };

        ExamEditPolicy.Decide(exam, request, hasAssignments: true, hasStartedAttempts: true)
            .FrozenFieldsChanged.Should().Equal("số câu hỏi", "điểm mỗi câu", "trộn thứ tự đáp án");
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
}
