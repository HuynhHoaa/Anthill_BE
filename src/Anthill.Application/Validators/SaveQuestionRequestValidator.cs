using Anthill.Application.DTOs.Questions;
using Anthill.Application.Mappings;
using Anthill.Domain.Constants;
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

        // Chỉ nhận đường dẫn tương đối do chính /api/uploads/images cấp. Không chặn thì một lời
        // gọi API trực tiếp (ngoài UI) có thể gán một URL tuyệt đối tới máy chủ khác — trình duyệt
        // của mọi học sinh mở đề sẽ tải URL đó, lộ IP và thời điểm làm bài cho bên thứ ba.
        RuleFor(request => request.ImageUrl)
            .Must(url => IsAllowedImageUrl(url))
            .WithMessage("Đường dẫn ảnh phải là ảnh đã tải lên qua /api/uploads/images.")
            .When(request => !string.IsNullOrWhiteSpace(request.ImageUrl));

        RuleFor(request => request.Answers)
            .Must(answers => answers is { Count: >= MinAnswers })
            .WithMessage($"Câu hỏi phải có ít nhất {MinAnswers} đáp án.");

        RuleFor(request => request.Answers)
            .Must(answers => answers!.Count <= MaxAnswers)
            .WithMessage($"Câu hỏi không được có quá {MaxAnswers} đáp án.")
            .When(HasAnswers);

        // Phần tử null trong mảng JSON ("answers": [null, ...]) qua được bước bind model. Các quy tắc
        // phía dưới đọc thuộc tính của từng phần tử nên phải loại nó ra trước (HasAnswers cũng đã lọc),
        // không thì một lỗi 400 biến thành 500.
        RuleFor(request => request.Answers)
            .Must(answers => answers!.All(answer => answer is not null))
            .WithMessage("Danh sách đáp án có một dòng trống.")
            .When(request => request.Answers is { Count: > 0 });

        RuleFor(request => request.Answers)
            .Must(answers => answers!.All(HasSomeContent))
            .WithMessage("Nội dung đáp án không được để trống.")
            .When(HasAnswers);

        // Cột AnswerOptions.ImageUrl dài 512 như cột của câu hỏi; không chặn thì đường dẫn dài hơn đi
        // thẳng xuống database và vỡ ở đó.
        RuleFor(request => request.Answers)
            .Must(answers => answers!.All(answer =>
                string.IsNullOrWhiteSpace(answer.ImageUrl) || answer.ImageUrl.Trim().Length <= MaxImageUrlLength))
            .WithMessage($"Đường dẫn ảnh đáp án không được vượt quá {MaxImageUrlLength} ký tự.")
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

        RuleFor(request => request.Answers)
            .Must(answers => answers!.All(answer => IsAllowedImageUrl(answer.ImageUrl)))
            .WithMessage("Đường dẫn ảnh đáp án phải là ảnh đã tải lên qua /api/uploads/images.")
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

    private static bool HasAnswers(SaveQuestionRequest request) =>
        request.Answers is { Count: > 0 } answers && answers.All(answer => answer is not null);

    /// <summary>Đáp án hợp lệ khi có chữ <b>hoặc</b> có ảnh — một đáp án chỉ gồm hình là bình thường.</summary>
    private static bool HasSomeContent(SaveAnswerOptionRequest answer) =>
        !string.IsNullOrWhiteSpace(answer.Content) || !string.IsNullOrWhiteSpace(answer.ImageUrl);

    private static bool IsDataUri(string? value) =>
        value is not null && value.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Trống thì hợp lệ (ảnh không bắt buộc); có giá trị thì phải là đường dẫn tương đối do chính
    /// <c>/api/uploads/images</c> cấp — không phải một URL tuyệt đối tới máy chủ khác.
    /// </summary>
    private static bool IsAllowedImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return value.Trim().StartsWith(
            $"{FileStorageConstants.PublicRequestPathPrefix}/", StringComparison.Ordinal);
    }
}
