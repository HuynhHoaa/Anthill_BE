using Anthill.Application.DTOs.Questions;
using Anthill.Application.Mappings;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Quy tắc lấy từ <c>questionService.validatePayload</c> của frontend, cộng giới hạn độ dài của
/// <c>QuestionConfiguration</c> và <c>AnswerOptionConfiguration</c>.
/// </summary>
/// <remarks>
/// <para>
/// Hai quy tắc quan trọng nhất là <b>ít nhất hai đáp án</b> và <b>đúng một đáp án đúng</b>. Chúng
/// không phải chuyện thẩm mỹ: bước 6 chấm bài bằng cách đối chiếu đúng một <c>AnswerOption</c> có
/// <c>IsCorrect = true</c>. Một câu hỏi không có đáp án đúng nào thì không học sinh nào ăn điểm
/// được, còn hai đáp án đúng thì kết quả phụ thuộc vào thứ tự dòng trong database.
/// </para>
/// <para>
/// Đáp án được coi là có nội dung khi <b>có chữ hoặc có ảnh</b> — một đáp án chỉ gồm hình là hợp
/// lệ, đúng như frontend đã chấp nhận.
/// </para>
/// </remarks>
public sealed class SaveQuestionRequestValidator : AbstractValidator<SaveQuestionRequest>
{
    public const int MaxContentLength = 2000;
    public const int MaxTopicLength = 128;
    public const int MaxAnswerContentLength = 1000;
    public const int MaxImageUrlLength = 512;

    public const int MinAnswers = 2;

    /// <summary>Trần thực dụng: quá số này thì màn hình làm bài không còn đọc được trên điện thoại.</summary>
    public const int MaxAnswers = 10;

    /// <summary>Cột <c>DefaultPoint</c> khai <c>precision(6, 2)</c> nên 9999.99 là giá trị lớn nhất lưu được.</summary>
    public const decimal MaxDefaultPoint = 9999.99m;

    public SaveQuestionRequestValidator()
    {
        RuleFor(request => request.Content)
            .Must(content => !string.IsNullOrWhiteSpace(content))
            .WithMessage("Nội dung câu hỏi không được để trống.");

        RuleFor(request => request.Content)
            .Must(content => content!.Trim().Length <= MaxContentLength)
            .WithMessage($"Nội dung câu hỏi không được vượt quá {MaxContentLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.Content));

        RuleFor(request => request.Topic)
            .Must(topic => !string.IsNullOrWhiteSpace(topic))
            .WithMessage("Vui lòng chọn hoặc nhập chủ đề cho câu hỏi.");

        RuleFor(request => request.Topic)
            .Must(topic => topic!.Trim().Length <= MaxTopicLength)
            .WithMessage($"Chủ đề không được vượt quá {MaxTopicLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.Topic));

        RuleFor(request => request.Difficulty)
            .Must(difficulty => QuestionMappingExtensions.TryParseDifficulty(difficulty, out _))
            .WithMessage("Mức độ khó chỉ nhận giá trị 'easy', 'medium' hoặc 'hard'.");

        RuleFor(request => request.DefaultPoint)
            .GreaterThan(0)
            .WithMessage("Điểm mặc định phải lớn hơn 0.");

        RuleFor(request => request.DefaultPoint)
            .LessThanOrEqualTo(MaxDefaultPoint)
            .WithMessage($"Điểm mặc định không được vượt quá {MaxDefaultPoint}.")
            .When(request => request.DefaultPoint > 0);

        RuleFor(request => request.Status)
            .Must(status => QuestionMappingExtensions.TryParseQuestionStatus(status, out _))
            .WithMessage("Trạng thái câu hỏi chỉ nhận giá trị 'active' hoặc 'hidden'.")
            .When(request => !string.IsNullOrWhiteSpace(request.Status));

        RuleFor(request => request.Visibility)
            .Must(visibility => QuestionMappingExtensions.TryParseVisibility(visibility, out _))
            .WithMessage("Phạm vi chia sẻ chỉ nhận giá trị 'shared' hoặc 'private'.")
            .When(request => !string.IsNullOrWhiteSpace(request.Visibility));

        RuleFor(request => request.ImageUrl)
            .Must(url => url!.Trim().Length <= MaxImageUrlLength)
            .WithMessage($"Đường dẫn ảnh không được vượt quá {MaxImageUrlLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.ImageUrl));

        // §1: ảnh phải upload trước qua POST /api/uploads/images rồi mới gửi URL. Data URI lọt vào
        // đây sẽ vượt 512 ký tự và vỡ ở database — báo đúng nguyên nhân hơn là để nó vỡ ở đó.
        RuleFor(request => request.ImageUrl)
            .Must(url => !IsDataUri(url))
            .WithMessage("Ảnh phải được tải lên qua /api/uploads/images trước; API không nhận data URI.")
            .When(request => !string.IsNullOrWhiteSpace(request.ImageUrl));

        RuleFor(request => request.Answers)
            .Must(answers => answers is { Count: >= MinAnswers })
            .WithMessage($"Câu hỏi phải có ít nhất {MinAnswers} đáp án.");

        RuleFor(request => request.Answers)
            .Must(answers => answers!.Count <= MaxAnswers)
            .WithMessage($"Câu hỏi không được có quá {MaxAnswers} đáp án.")
            .When(HasAnswers);

        RuleFor(request => request.Answers)
            .Must(answers => answers!.All(HasSomeContent))
            .WithMessage("Nội dung đáp án không được để trống.")
            .When(HasAnswers);

        RuleFor(request => request.Answers)
            .Must(answers => answers!.All(answer =>
                string.IsNullOrWhiteSpace(answer.Content) || answer.Content.Trim().Length <= MaxAnswerContentLength))
            .WithMessage($"Nội dung đáp án không được vượt quá {MaxAnswerContentLength} ký tự.")
            .When(HasAnswers);

        RuleFor(request => request.Answers)
            .Must(answers => answers!.All(answer => !IsDataUri(answer.ImageUrl)))
            .WithMessage("Ảnh đáp án phải được tải lên qua /api/uploads/images trước; API không nhận data URI.")
            .When(HasAnswers);

        // Hai vế tách riêng để thông điệp nói đúng việc người dùng phải làm: chưa chọn đáp án đúng
        // và chọn nhầm hai đáp án đúng là hai thao tác sửa khác nhau.
        RuleFor(request => request.Answers)
            .Must(answers => answers!.Any(answer => answer.IsCorrect))
            .WithMessage("Vui lòng chọn một đáp án đúng.")
            .When(HasAnswers);

        RuleFor(request => request.Answers)
            .Must(answers => answers!.Count(answer => answer.IsCorrect) <= 1)
            .WithMessage("Mỗi câu hỏi chỉ được có đúng một đáp án đúng.")
            .When(HasAnswers);
    }

    private static bool HasAnswers(SaveQuestionRequest request) => request.Answers is { Count: > 0 };

    /// <summary>Đáp án hợp lệ khi có chữ <b>hoặc</b> có ảnh — một đáp án chỉ gồm hình là bình thường.</summary>
    private static bool HasSomeContent(SaveAnswerOptionRequest answer) =>
        !string.IsNullOrWhiteSpace(answer.Content) || !string.IsNullOrWhiteSpace(answer.ImageUrl);

    private static bool IsDataUri(string? value) =>
        value is not null && value.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase);
}
