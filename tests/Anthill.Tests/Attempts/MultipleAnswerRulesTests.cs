using Anthill.Application.Common;
using Anthill.Application.DTOs.ExamAttempts;
using Anthill.Application.DTOs.Questions;
using Anthill.Application.Validators;
using FluentAssertions;

namespace Anthill.Tests.Attempts;

/// <summary>
/// Luật của câu cho chọn nhiều đáp án đúng, phần chặn được trước khi chạm database: soạn câu hỏi, gói lưu
/// nháp, và cách hợp nhất hai kiểu gửi lựa chọn.
/// </summary>
public class MultipleAnswerRulesTests
{
    private static SaveAnswerOptionRequest Option(string content, bool correct = false) =>
        new() { Content = content, IsCorrect = correct };

    private static SaveQuestionRequest Question(bool allowMultiple, params bool[] correctFlags) => new()
    {
        Content = "Chọn các số nguyên tố",
        Topic = "Số học",
        Difficulty = "easy",
        DefaultPoint = 1m,
        AllowMultiple = allowMultiple,
        Answers = correctFlags.Select((flag, index) => Option($"Phương án {index + 1}", flag)).ToList()
    };

    private static IReadOnlyList<string> ErrorsOf(SaveQuestionRequest request) =>
        new SaveQuestionRequestValidator().Validate(request).Errors.Select(failure => failure.ErrorMessage).ToList();

    [Fact]
    public void Cau_mot_dap_an_dung_van_phai_co_dung_mot_o_dung()
    {
        new SaveQuestionRequestValidator().Validate(Question(false, true, false, false, false)).IsValid.Should().BeTrue();

        ErrorsOf(Question(false, true, true, false, false))
            .Should().Contain(message => message.Contains("chỉ được có đúng một đáp án đúng"));
        ErrorsOf(Question(false, false, false, false, false))
            .Should().Contain("Vui lòng chọn một đáp án đúng.");
    }

    [Fact]
    public void Cau_nhieu_dap_an_cho_phep_nhieu_o_dung()
    {
        new SaveQuestionRequestValidator().Validate(Question(true, true, true, false, true)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Cau_nhieu_dap_an_chi_co_mot_o_dung_van_hop_le()
    {
        // "Chọn tất cả đáp án đúng" có thể chỉ có một ô đúng, và đó là cách đặt câu hỏi hợp lệ.
        new SaveQuestionRequestValidator().Validate(Question(true, false, true, false, false)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Cau_nhieu_dap_an_khong_co_o_dung_nao_bi_tu_choi_voi_cau_chu_rieng()
    {
        ErrorsOf(Question(true, false, false, false, false))
            .Should().Contain("Vui lòng chọn ít nhất một đáp án đúng.");
    }

    [Fact]
    public void Cau_hoi_gui_len_khong_kem_allowMultiple_la_cau_mot_dap_an()
    {
        var request = Question(false, true, false);

        request.AllowMultiple.Should().BeFalse();
    }

    // ── gói lưu nháp ─────────────────────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<string> SaveErrors(params AnswerSelectionDto[] answers) =>
        new SaveAnswersRequestValidator()
            .Validate(new SaveAnswersRequest { ClientSeq = 1, Answers = answers })
            .Errors.Select(failure => failure.ErrorMessage).ToList();

    [Fact]
    public void Gui_tap_dap_an_hop_le()
    {
        SaveErrors(new AnswerSelectionDto { QuestionId = 10, AnswerIds = [101, 103] }).Should().BeEmpty();
    }

    [Fact]
    public void Gui_mang_rong_la_bo_chon_het()
    {
        SaveErrors(new AnswerSelectionDto { QuestionId = 10, AnswerIds = [] }).Should().BeEmpty();
    }

    [Fact]
    public void Ma_dap_an_khong_duong_trong_mang_bi_tu_choi()
    {
        SaveErrors(new AnswerSelectionDto { QuestionId = 10, AnswerIds = [101, 0] })
            .Should().Contain("Danh sách câu trả lời chứa mã đáp án không hợp lệ.");
        SaveErrors(new AnswerSelectionDto { QuestionId = 10, AnswerIds = [-4] })
            .Should().Contain("Danh sách câu trả lời chứa mã đáp án không hợp lệ.");
    }

    [Fact]
    public void Gui_ca_answerId_lan_answerIds_bi_tu_choi_thay_vi_im_lang_chon_mot_ben()
    {
        SaveErrors(new AnswerSelectionDto { QuestionId = 10, AnswerId = 101, AnswerIds = [102] })
            .Should().Contain("Mỗi câu chỉ gửi answerId hoặc answerIds, không gửi cả hai.");
    }

    [Fact]
    public void Qua_nhieu_o_cho_mot_cau_bi_tu_choi()
    {
        var tooMany = Enumerable.Range(1, AnswerSelectionRules.MaxAnswersPerSelection + 1).ToList();

        SaveErrors(new AnswerSelectionDto { QuestionId = 10, AnswerIds = tooMany })
            .Should().Contain($"Mỗi câu chỉ chọn được tối đa {AnswerSelectionRules.MaxAnswersPerSelection} đáp án.");
    }

    // ── hợp nhất hai kiểu gửi ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Kieu_cu_answerId_thanh_tap_mot_phan_tu_hoac_rong()
    {
        new AnswerSelectionDto { QuestionId = 1, AnswerId = 7 }.SelectedIds().Should().Equal(7);
        new AnswerSelectionDto { QuestionId = 1, AnswerId = null }.SelectedIds().Should().BeEmpty();
    }

    [Fact]
    public void Kieu_moi_answerIds_duoc_khu_trung_va_giu_thu_tu()
    {
        new AnswerSelectionDto { QuestionId = 1, AnswerIds = [9, 4, 9, 6] }.SelectedIds().Should().Equal(9, 4, 6);
    }
}
