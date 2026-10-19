using System.Linq.Expressions;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Bộ luật chung cho mọi lý do quản trị viên phải nhập: khoá tài khoản, xoá tài khoản, gỡ khỏi lớp, chuyển lớp.
/// </summary>
/// <remarks>
/// Một bộ luật cho cả bốn việc để người dùng không phải học bốn mức khác nhau, và để lý do của một lần khoá hay
/// chuyển lớp đọc được như lý do xoá tài khoản đã có từ trước.
/// </remarks>
public static class ReasonRules
{
    /// <summary>
    /// Số ký tự tối thiểu, đo sau khi cắt khoảng trắng hai đầu. Đủ dài để loại "xoá", "ok", "abc" mà vẫn không cản
    /// người có việc thật.
    /// </summary>
    public const int MinimumLength = 10;

    /// <summary>Số ký tự tối đa, bằng độ dài cột lưu lý do.</summary>
    public const int MaximumLength = 512;

    /// <summary>
    /// Áp luật cho một trường lý do bắt buộc. Đo sau khi cắt khoảng trắng: service lưu bản đã cắt, nên "a" kèm chín
    /// dấu cách không được lọt qua mức tối thiểu rồi bị lưu thành một chữ.
    /// </summary>
    /// <summary>
    /// Lý do không bắt buộc ở tầng này (có trường hợp không cần, và chỉ service biết), nhưng đã nhập thì phải đúng độ dài.
    /// </summary>
    public static void ApplyOptional<T>(AbstractValidator<T> validator, Expression<Func<T, string?>> reason)
    {
        var read = reason.Compile();

        validator.RuleFor(reason)
            .Must(value => value!.Trim().Length >= MinimumLength)
                .WithMessage($"Lý do cần ít nhất {MinimumLength} ký tự để người đọc sau này hiểu được.")
            .Must(value => value!.Trim().Length <= MaximumLength)
                .WithMessage($"Lý do tối đa {MaximumLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(read(request)));
    }

    public static void ApplyRequired<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string?>> reason,
        string emptyMessage)
    {
        var read = reason.Compile();

        validator.RuleFor(reason)
            .NotEmpty().WithMessage(emptyMessage);

        validator.RuleFor(reason)
            .Must(value => value!.Trim().Length >= MinimumLength)
                .WithMessage($"Lý do cần ít nhất {MinimumLength} ký tự để người đọc sau này hiểu được.")
            .Must(value => value!.Trim().Length <= MaximumLength)
                .WithMessage($"Lý do tối đa {MaximumLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(read(request)));
    }
}
