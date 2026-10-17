using Anthill.Application.DTOs.Questions;
using Anthill.Application.Mappings;
using Anthill.Application.Validators;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Exams;

/// <summary>
/// Tên câu hỏi tách khỏi nội dung: không bắt buộc, một dòng, có trần độ dài, và không bao giờ null.
/// </summary>
public class QuestionTitleTests
{
    private static SaveQuestionRequest Request(string? title) => new()
    {
        Title = title,
        Content = "Hai cộng hai bằng mấy?",
        Topic = "Số học",
        Difficulty = "easy",
        DefaultPoint = 1m,
        Answers =
        [
            new SaveAnswerOptionRequest { Content = "Bốn", IsCorrect = true },
            new SaveAnswerOptionRequest { Content = "Năm" }
        ]
    };

    [Fact]
    public void Khong_dat_ten_van_hop_le()
    {
        new SaveQuestionRequestValidator().Validate(Request(null)).IsValid.Should().BeTrue();
        new SaveQuestionRequestValidator().Validate(Request("   ")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Ten_dung_bang_tran_hop_le_va_vuot_tran_bi_tu_choi()
    {
        var atLimit = new string('a', SaveQuestionRequestValidator.MaxTitleLength);
        new SaveQuestionRequestValidator().Validate(Request(atLimit)).IsValid.Should().BeTrue();

        var result = new SaveQuestionRequestValidator().Validate(Request(atLimit + "b"));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(failure => failure.ErrorMessage)
            .Should().Contain($"Tên câu hỏi không được vượt quá {SaveQuestionRequestValidator.MaxTitleLength} ký tự.");
    }

    [Fact]
    public void Do_dai_duoc_do_sau_khi_gop_khoang_trang()
    {
        // 60 từ cách nhau bằng nhiều dấu cách / xuống dòng: thô dài 415, chuẩn hoá còn 179 nên vẫn trong trần.
        var padded = string.Join("  \n  ", Enumerable.Repeat("ab", 60));

        padded.Length.Should().BeGreaterThan(SaveQuestionRequestValidator.MaxTitleLength);
        new SaveQuestionRequestValidator().Validate(Request(padded)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   \n\t ", "")]
    [InlineData("  Định luật Ôm  ", "Định luật Ôm")]
    [InlineData("Định luật\nÔm\t\tphần 1", "Định luật Ôm phần 1")]
    public void Ten_duoc_chuan_hoa_thanh_mot_dong(string? input, string expected)
    {
        QuestionMappingExtensions.NormalizeTitle(input).Should().Be(expected);
    }

    [Fact]
    public void Dto_tra_ten_dung_nhu_da_luu_ke_ca_khi_rong()
    {
        var named = new Question
        {
            Title = "Tính điện trở tương đương",
            Content = "Cho hai điện trở mắc nối tiếp…",
            Topic = "Điện học",
            Difficulty = QuestionDifficulty.Medium
        };
        var unnamed = new Question
        {
            Content = "Cho hai điện trở mắc nối tiếp…",
            Topic = "Điện học",
            Difficulty = QuestionDifficulty.Medium
        };

        named.ToDto(canEdit: true).Title.Should().Be("Tính điện trở tương đương");

        // Rỗng chứ không phải null và cũng không bị thay bằng đoạn đầu nội dung: việc rơi về nội dung
        // thuộc về nơi hiển thị, để màn sửa vẫn biết câu này chưa được đặt tên.
        unnamed.ToDto(canEdit: true).Title.Should().BeEmpty();
    }
}
