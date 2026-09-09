using EduQuiz.Application.DTOs.Exams;
using EduQuiz.Application.Validators;
using FluentAssertions;

namespace EduQuiz.Tests.Exams;

/// <summary>
/// Kiểm hình dạng của request tạo/sửa đề, gồm hai luật mà frontend mới chỉ cảnh báo ở giao diện.
/// </summary>
public class SaveExamRequestValidatorTests
{
    private static readonly DateTime OpenAt = new(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);

    private readonly SaveExamRequestValidator _validator = new();

    /// <summary>Một request hợp lệ, để mỗi ca test chỉ đổi đúng thứ nó muốn kiểm.</summary>
    private static SaveExamRequest Valid(
        string title = "Kiểm tra giữa kỳ",
        int durationMinutes = 45,
        int totalQuestions = 20,
        int easy = 40,
        int medium = 40,
        int hard = 20,
        decimal pointPerQuestion = 0.5m,
        int windowHours = 2,
        string? lifecycle = null) => new()
    {
        Title = title,
        Description = "Nội dung chương 1 và 2",
        Topic = "Đại số",
        DurationMinutes = durationMinutes,
        TotalQuestions = totalQuestions,
        EasyPercentage = easy,
        MediumPercentage = medium,
        HardPercentage = hard,
        PointPerQuestion = pointPerQuestion,
        OpenAt = OpenAt,
        CloseAt = OpenAt.AddHours(windowHours),
        Lifecycle = lifecycle
    };

    private IReadOnlyList<string> ErrorsOf(SaveExamRequest request) =>
        _validator.Validate(request).Errors.Select(failure => failure.ErrorMessage).ToList();

    [Fact]
    public void Request_hop_le_thi_khong_co_loi_nao()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// Luật frontend chỉ cảnh báo chứ không chặn: thời lượng dài hơn cửa sổ mở đề (§5).
    /// </summary>
    /// <remarks>
    /// Cho làm bài 90 phút trong cửa sổ mở 30 phút nghĩa là mọi học sinh đều bị cắt giữa chừng —
    /// một cấu hình không ai muốn lưu, nhưng giao diện hiện tại vẫn cho lưu.
    /// </remarks>
    [Fact]
    public void Thoi_luong_dai_hon_cua_so_mo_de_thi_bi_tu_choi()
    {
        var result = _validator.Validate(Valid(durationMinutes: 90, windowHours: 1));

        result.IsValid.Should().BeFalse();
        ErrorsOf(Valid(durationMinutes: 90, windowHours: 1))
            .Should().Contain(message => message.Contains("dài hơn khoảng thời gian mở đề"));
    }

    [Fact]
    public void Thoi_luong_bang_dung_cua_so_mo_de_thi_hop_le()
    {
        // Đúng bằng thì vẫn làm hết bài được, nên đây là biên hợp lệ chứ không phải biên lỗi.
        _validator.Validate(Valid(durationMinutes: 120, windowHours: 2)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Cua_so_nguoc_chi_bao_dung_mot_loi_ve_thu_tu_hai_moc()
    {
        // Cửa sổ âm thì đương nhiên không chứa nổi thời lượng nào, nhưng báo cả hai câu cùng lúc
        // chỉ làm người dùng rối. Quy tắc thời lượng cố ý im lặng ở tình huống này.
        var errors = ErrorsOf(Valid(windowHours: -1));

        errors.Should().Contain(message => message.Contains("phải sau thời gian mở bài"));
        errors.Should().NotContain(message => message.Contains("dài hơn khoảng thời gian mở đề"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("draft")]
    [InlineData("DRAFT")]
    [InlineData(" draft ")]
    public void Lifecycle_chi_nhan_draft_hoac_bo_trong(string? lifecycle)
    {
        _validator.Validate(Valid(lifecycle: lifecycle)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("scheduled")]
    [InlineData("open")]
    [InlineData("closed")]
    public void Lifecycle_dan_xuat_gui_len_thi_bi_tu_choi(string lifecycle)
    {
        // Ba giá trị này tính từ mốc thời gian. Nhận chúng là cho phép ép một đề sang trạng thái
        // mâu thuẫn với chính lịch của nó.
        var errors = ErrorsOf(Valid(lifecycle: lifecycle));

        errors.Should().Contain(message => message.Contains("chỉ nhận giá trị 'draft'"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Tieu_de_rong_hoac_toan_khoang_trang_bi_tu_choi(string title)
    {
        ErrorsOf(Valid(title: title))
            .Should().Contain(message => message.Contains("không được để trống"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    [InlineData(301)]
    public void Thoi_gian_lam_bai_ngoai_khoang_1_den_300_bi_tu_choi(int durationMinutes)
    {
        // windowHours đủ rộng để không dính luật cửa sổ, nhằm cô lập đúng luật đang kiểm.
        ErrorsOf(Valid(durationMinutes: durationMinutes, windowHours: 24))
            .Should().Contain(message => message.Contains("Thời gian làm bài phải từ 1 đến 300 phút"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Diem_moi_cau_khong_duong_bi_tu_choi(decimal pointPerQuestion)
    {
        ErrorsOf(Valid(pointPerQuestion: pointPerQuestion))
            .Should().Contain(message => message.Contains("Điểm mỗi câu phải lớn hơn 0"));
    }

    [Fact]
    public void So_cau_duoi_1_bi_tu_choi()
    {
        ErrorsOf(Valid(totalQuestions: 0))
            .Should().Contain(message => message.Contains("Số câu hỏi của đề phải từ 1"));
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(101)]
    public void Ty_le_ngoai_khoang_0_den_100_bi_tu_choi(int easy)
    {
        ErrorsOf(Valid(easy: easy))
            .Should().Contain(message => message.Contains("Tỷ lệ câu Dễ"));
    }

    [Fact]
    public void Tong_ba_ty_le_khac_100_khong_bi_validator_chan()
    {
        // Cố ý: bản nháp được phép lưu với tổng chưa bằng 100. Phép kiểm đó phụ thuộc việc đề có
        // phải nháp hay không nên thuộc về ExamService, không thuộc về validator.
        _validator.Validate(Valid(easy: 10, medium: 10, hard: 10)).IsValid.Should().BeTrue();
    }
}
