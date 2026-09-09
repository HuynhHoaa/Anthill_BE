using EduQuiz.Application.DTOs.ExamAttempts;
using EduQuiz.Application.Validators;
using FluentAssertions;

namespace EduQuiz.Tests.Attempts;

/// <summary>
/// Kiểm hình dạng gói lưu nháp — phần chặn được trước khi chạm database.
/// </summary>
/// <remarks>
/// Ba ca ở đây từng là ba lỗi 500 chờ sẵn: danh sách <c>null</c> do <c>System.Text.Json</c> ghi đè
/// giá trị mặc định, <c>clientSeq</c> bỏ trống rơi vào đúng bộ lọc chống ghi đè ngược thứ tự, và
/// hai dòng cho cùng một câu đâm vào unique index <c>(AttemptId, ExamPaperQuestionId)</c>.
/// </remarks>
public class SaveAnswersRequestValidatorTests
{
    private readonly SaveAnswersRequestValidator _validator = new();

    private static SaveAnswersRequest Valid(long clientSeq = 1, IReadOnlyList<AnswerSelectionDto>? answers = null) =>
        new()
        {
            ClientSeq = clientSeq,
            Answers = answers ?? [new AnswerSelectionDto { QuestionId = 10, AnswerId = 101 }]
        };

    private IReadOnlyList<string> ErrorsOf(SaveAnswersRequest request) =>
        _validator.Validate(request).Errors.Select(failure => failure.ErrorMessage).ToList();

    [Fact]
    public void Goi_hop_le_thi_khong_co_loi_nao()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Danh_sach_rong_van_hop_le()
    {
        // Client gửi một gói rỗng chỉ để nhích clientSeq là chuyện bình thường của autosave.
        _validator.Validate(Valid(answers: [])).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Danh_sach_null_bi_tu_choi_chu_khong_thanh_loi_500()
    {
        // DTO khai mặc định là mảng rỗng, nhưng System.Text.Json ghi đè bằng null khi client gửi
        // thẳng "answers": null — đúng cái bẫy đã làm hỏng endpoint giao bài ở bước 5.
        var request = new SaveAnswersRequest { ClientSeq = 1, Answers = null! };

        ErrorsOf(request).Should().Contain("Danh sách câu trả lời không hợp lệ.");
    }

    [Fact]
    public void Bo_trong_answerId_la_hop_le_vi_do_la_thao_tac_bo_chon()
    {
        var request = Valid(answers: [new AnswerSelectionDto { QuestionId = 10, AnswerId = null }]);

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ClientSeq_khong_duong_bi_tu_choi(long clientSeq)
    {
        // LastAppliedSeq của một lượt mới bằng 0, nên một gói mang clientSeq = 0 sẽ bị chính bộ lọc
        // chống ghi đè ngược thứ tự loại bỏ trong im lặng — client tưởng mình đang lưu bài suốt
        // buổi trong khi không có gì được ghi.
        ErrorsOf(Valid(clientSeq)).Should().Contain("Thiếu số thứ tự gói tin của phiên làm bài.");
    }

    [Fact]
    public void Hai_dong_cho_cung_mot_cau_bi_tu_choi()
    {
        var request = Valid(answers:
        [
            new AnswerSelectionDto { QuestionId = 10, AnswerId = 101 },
            new AnswerSelectionDto { QuestionId = 10, AnswerId = 102 }
        ]);

        ErrorsOf(request).Should().Contain("Một câu hỏi chỉ được xuất hiện một lần trong mỗi lần gửi.");
    }

    [Fact]
    public void Ma_cau_hoi_khong_duong_bi_tu_choi()
    {
        var request = Valid(answers: [new AnswerSelectionDto { QuestionId = 0, AnswerId = 101 }]);

        ErrorsOf(request).Should().Contain("Danh sách câu trả lời chứa mã câu hỏi không hợp lệ.");
    }

    [Fact]
    public void Ma_dap_an_khong_duong_bi_tu_choi()
    {
        var request = Valid(answers: [new AnswerSelectionDto { QuestionId = 10, AnswerId = -1 }]);

        ErrorsOf(request).Should().Contain("Danh sách câu trả lời chứa mã đáp án không hợp lệ.");
    }

    [Fact]
    public void Goi_qua_lon_bi_tu_choi()
    {
        var answers = Enumerable
            .Range(1, AnswerSelectionRules.MaxSelectionsPerRequest + 1)
            .Select(id => new AnswerSelectionDto { QuestionId = id, AnswerId = id * 10 })
            .ToList();

        ErrorsOf(Valid(answers: answers)).Should()
            .Contain($"Mỗi lần chỉ gửi được tối đa {AnswerSelectionRules.MaxSelectionsPerRequest} câu.");
    }

    /// <summary>
    /// Request nộp bài dùng chung bộ luật đó, chỉ khác ở chỗ <c>clientSeq</c> là tuỳ chọn.
    /// </summary>
    [Fact]
    public void Nop_bai_khong_bat_buoc_clientSeq_nhung_van_chan_trung_cau()
    {
        var validator = new SubmitAttemptRequestValidator();

        validator.Validate(new SubmitAttemptRequest()).IsValid.Should().BeTrue();

        var duplicated = new SubmitAttemptRequest
        {
            Answers =
            [
                new AnswerSelectionDto { QuestionId = 10, AnswerId = 101 },
                new AnswerSelectionDto { QuestionId = 10, AnswerId = 102 }
            ]
        };

        validator.Validate(duplicated).IsValid.Should().BeFalse();
    }
}
