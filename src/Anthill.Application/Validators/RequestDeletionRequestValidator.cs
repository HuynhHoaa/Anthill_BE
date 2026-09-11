using Anthill.Application.DTOs.Students;
using FluentValidation;

namespace Anthill.Application.Validators;

public sealed class RequestDeletionRequestValidator : AbstractValidator<RequestDeletionRequest>
{
    /// <summary>Đủ dài để loại "xoá", "ok", "abc" mà vẫn không cản người có việc thật.</summary>
    public const int MinimumReasonLength = 10;

    public RequestDeletionRequestValidator()
    {
        RuleFor(request => request.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do xoá tài khoản.")
            .MinimumLength(MinimumReasonLength)
                .WithMessage($"Lý do cần ít nhất {MinimumReasonLength} ký tự để người đọc sau này hiểu được.")
            .MaximumLength(512).WithMessage("Lý do tối đa 512 ký tự.");
    }
}
