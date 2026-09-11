using Anthill.Application.DTOs.ExamAttempts;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Luật dùng chung cho danh sách lựa chọn của học sinh, ở cả lưu nháp lẫn nộp bài.
/// </summary>
/// <remarks>
/// Gộp về một chỗ vì hai request mang cùng một danh sách với cùng ràng buộc. Viết hai bản là hai
/// cơ hội để bản nộp bài quên mất phép chặn trùng <c>questionId</c> mà bản lưu nháp có — và đúng
/// phép chặn đó mới là thứ giữ cho unique index <c>(AttemptId, ExamPaperQuestionId)</c> không nổ.
/// </remarks>
public static class AnswerSelectionRules
{
    /// <summary>
    /// Trần số câu trong một gói tin.
    /// </summary>
    /// <remarks>
    /// Rộng hơn hẳn số câu tối đa của một đề để không bao giờ chặn nhầm người dùng thật; nó ở đây
    /// chỉ để một request nghìn phần tử không kéo theo nghìn dòng ghi.
    /// </remarks>
    public const int MaxSelectionsPerRequest = 500;

    /// <summary>
    /// Gắn bộ luật vào một danh sách lựa chọn.
    /// </summary>
    /// <remarks>
    /// Danh sách phải được <b>null-guard</b>: DTO khai mặc định là mảng rỗng nhưng
    /// <c>System.Text.Json</c> ghi đè giá trị đó bằng <c>null</c> khi client gửi thẳng
    /// <c>"answers": null</c>, và một quy tắc đọc <c>.Count</c> trên đó biến request sai định dạng
    /// thành lỗi 500.
    /// </remarks>
    public static void ApplyTo<TRequest>(
        AbstractValidator<TRequest> validator,
        Func<TRequest, IReadOnlyList<AnswerSelectionDto>?> selector,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(selector);

        validator.RuleFor(request => selector(request))
            .NotNull()
            .WithName(propertyName)
            .WithMessage("Danh sách câu trả lời không hợp lệ.");

        validator.RuleFor(request => selector(request))
            .Must(answers => answers!.Count <= MaxSelectionsPerRequest)
            .WithName(propertyName)
            .WithMessage($"Mỗi lần chỉ gửi được tối đa {MaxSelectionsPerRequest} câu.")
            .When(request => selector(request) is not null);

        validator.RuleFor(request => selector(request))
            .Must(answers => answers!.All(answer => answer.QuestionId > 0))
            .WithName(propertyName)
            .WithMessage("Danh sách câu trả lời chứa mã câu hỏi không hợp lệ.")
            .When(request => selector(request) is { Count: > 0 });

        validator.RuleFor(request => selector(request))
            .Must(answers => answers!.All(answer => answer.AnswerId is null or > 0))
            .WithName(propertyName)
            .WithMessage("Danh sách câu trả lời chứa mã đáp án không hợp lệ.")
            .When(request => selector(request) is { Count: > 0 });

        // Hai dòng cho cùng một câu trong một gói tin thì không có câu trả lời đúng nào cho "cuối
        // cùng em chọn ô nào", mà phía dưới còn có unique index (AttemptId, ExamPaperQuestionId)
        // chờ sẵn để biến sự mập mờ đó thành lỗi 500.
        validator.RuleFor(request => selector(request))
            .Must(answers => answers!.Select(answer => answer.QuestionId).Distinct().Count() == answers!.Count)
            .WithName(propertyName)
            .WithMessage("Một câu hỏi chỉ được xuất hiện một lần trong mỗi lần gửi.")
            .When(request => selector(request) is { Count: > 0 });
    }
}
