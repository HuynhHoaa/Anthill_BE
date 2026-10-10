using Anthill.Application.DTOs.Students;
using FluentValidation;

namespace Anthill.Application.Validators;

public sealed class RequestDeletionRequestValidator : AbstractValidator<RequestDeletionRequest>
{
    /// <summary>Đủ dài để loại "xoá", "ok", "abc" mà vẫn không cản người có việc thật.</summary>
    public const int MinimumReasonLength = 10;

    public RequestDeletionRequestValidator()
    {
        // Đo sau khi cắt khoảng trắng hai đầu: service lưu bản đã cắt, nên "a" kèm chín dấu cách không được
        // lọt qua mức 10 ký tự rồi bị lưu thành một chữ.
        RuleFor(request => request.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do xoá tài khoản.");

        RuleFor(request => request.Reason)
            .Must(reason => reason!.Trim().Length >= MinimumReasonLength)
                .WithMessage($"Lý do cần ít nhất {MinimumReasonLength} ký tự để người đọc sau này hiểu được.")
            .Must(reason => reason!.Trim().Length <= 512).WithMessage("Lý do tối đa 512 ký tự.")
            .When(request => !string.IsNullOrEmpty(request.Reason));
    }
}
