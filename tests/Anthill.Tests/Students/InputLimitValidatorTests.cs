using Anthill.Application.DTOs.Auth;
using Anthill.Application.DTOs.ExamAttempts;
using Anthill.Application.DTOs.Exams;
using Anthill.Application.DTOs.Questions;
using Anthill.Application.DTOs.Students;
using Anthill.Application.Validators;
using FluentAssertions;

namespace Anthill.Tests.Students;

/// <summary>
/// Những đầu vào từng đi lọt validator rồi vỡ ở bộ băm mật khẩu hay ở database, thành lỗi 500 thay
/// vì một câu báo lỗi gắn đúng ô nhập.
/// </summary>
public class InputLimitValidatorTests
{
    private static CreateStudentRequest NewStudent(string password) => new()
    {
        FullName = "Nguyễn Văn Bê",
        Email = "be.nv@anthill.edu.vn",
        Username = "be.nv",
        Code = "HS1234",
        Password = password
    };

    private static IReadOnlyList<string> ErrorsOf<T>(FluentValidation.AbstractValidator<T> validator, T request) =>
        validator.Validate(request).Errors.Select(failure => failure.ErrorMessage).ToList();

    [Fact]
    public void Mat_khau_ban_dau_toan_khoang_trang_bi_tu_choi()
    {
        // Bộ băm từ chối chuỗi chỉ gồm khoảng trắng, và trước đây validator cho nó lọt qua vì chuỗi
        // đủ sáu ký tự: kết quả là lỗi 500 lúc tạo học sinh.
        var errors = ErrorsOf(new CreateStudentRequestValidator(), NewStudent("      "));

        errors.Should().Contain("Mật khẩu ban đầu không được để trống.");
    }

    [Fact]
    public void Mat_khau_co_khoang_trang_o_dau_va_cuoi_van_hop_le()
    {
        // Mật khẩu không bị trim: khoảng trắng bao quanh là ký tự hợp lệ.
        new CreateStudentRequestValidator().Validate(NewStudent("  abc123  ")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Mat_khau_moi_toan_khoang_trang_bi_tu_choi()
    {
        var request = new ChangePasswordRequest { CurrentPassword = "matkhau-cu", NewPassword = "        " };

        ErrorsOf(new ChangePasswordRequestValidator(), request).Should().Contain("Vui lòng nhập mật khẩu mới.");
    }

    [Fact]
    public void Mo_ta_de_dai_hon_cot_trong_database_bi_tu_choi()
    {
        // Cột Exams.Description dài 1000. Trần cũ là 2000 nên mô tả 1500 ký tự lọt validator và vỡ ở đó.
        var request = ValidExam() with { Description = new string('a', 1001) };

        ErrorsOf(new SaveExamRequestValidator(), request)
            .Should().Contain($"Mô tả không được dài quá {SaveExamRequestValidator.MaxDescriptionLength} ký tự.");
    }

    [Fact]
    public void Mo_ta_de_dung_bang_tran_van_hop_le()
    {
        var request = ValidExam() with { Description = new string('a', 1000) };

        new SaveExamRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Email_ho_so_dai_hon_cot_bi_tu_choi()
    {
        var request = new UpdateProfileRequest
        {
            FullName = "Nguyễn Văn Bê",
            Email = new string('a', 260) + "@x.vn"
        };

        ErrorsOf(new UpdateProfileRequestValidator(), request)
            .Should().Contain("Email không được vượt quá 256 ký tự.");
    }

    [Fact]
    public void Duong_dan_anh_dap_an_dai_hon_cot_bi_tu_choi()
    {
        var longUrl = "/uploads/" + new string('a', 520);
        var request = ValidQuestion(
            new SaveAnswerOptionRequest { Content = "Đúng", IsCorrect = true },
            new SaveAnswerOptionRequest { Content = "Sai", ImageUrl = longUrl });

        ErrorsOf(new SaveQuestionRequestValidator(), request)
            .Should().Contain("Đường dẫn ảnh đáp án không được vượt quá 512 ký tự.");
    }

    [Fact]
    public void Dap_an_null_trong_mang_bi_tu_choi_chu_khong_thanh_loi_500()
    {
        // "answers": [null, {...}] qua được bước bind model; các quy tắc đọc thuộc tính từng phần tử.
        var request = ValidQuestion(null!, new SaveAnswerOptionRequest { Content = "Đúng", IsCorrect = true });

        var result = new SaveQuestionRequestValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(failure => failure.ErrorMessage).Should().Contain("Danh sách đáp án có một dòng trống.");
    }

    [Fact]
    public void Cau_tra_loi_null_trong_goi_luu_nhap_bi_tu_choi_chu_khong_thanh_loi_500()
    {
        var request = new SaveAnswersRequest
        {
            ClientSeq = 1,
            Answers = [null!, new AnswerSelectionDto { QuestionId = 10, AnswerId = 101 }]
        };

        var result = new SaveAnswersRequestValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(failure => failure.ErrorMessage)
            .Should().Contain("Danh sách câu trả lời không hợp lệ.");
    }

    [Fact]
    public void Ly_do_xoa_chi_co_mot_chu_va_nhieu_dau_cach_bi_tu_choi()
    {
        // Service lưu bản đã cắt khoảng trắng, nên độ dài phải đo trên bản đã cắt.
        var request = new RequestDeletionRequest { Reason = "a" + new string(' ', 9) };

        ErrorsOf(new RequestDeletionRequestValidator(), request)
            .Should().Contain(message => message.Contains("ít nhất"));
    }

    [Fact]
    public void Ho_ten_hoc_sinh_dai_128_ky_tu_van_luu_duoc_so_dien_thoai()
    {
        // Quản trị viên tạo được tên tới 128 ký tự; trần hồ sơ cũ là 100 nên em đó không lưu nổi gì.
        var request = new UpdateProfileRequest
        {
            FullName = new string('a', 128),
            Email = "em@kiemthu.vn",
            Phone = "0912345678"
        };

        new UpdateProfileRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    private static SaveExamRequest ValidExam() => new()
    {
        Title = "Kiểm tra giữa kỳ",
        Description = "Nội dung chương 1 và 2",
        Topic = "Đại số",
        DurationMinutes = 45,
        TotalQuestions = 20,
        EasyPercentage = 40,
        MediumPercentage = 40,
        HardPercentage = 20,
        PointPerQuestion = 0.5m,
        OpenAt = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
        CloseAt = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc)
    };

    private static SaveQuestionRequest ValidQuestion(params SaveAnswerOptionRequest[] answers) => new()
    {
        Content = "Hai cộng hai bằng mấy?",
        Topic = "Số học",
        Difficulty = "easy",
        DefaultPoint = 1m,
        Answers = answers
    };
}
