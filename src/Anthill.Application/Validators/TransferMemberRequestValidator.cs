using Anthill.Application.DTOs.StudentGroups;
using FluentValidation;

namespace Anthill.Application.Validators;

public sealed class TransferMemberRequestValidator : AbstractValidator<TransferMemberRequest>
{
    public TransferMemberRequestValidator()
    {
        RuleFor(request => request.TargetGroupId)
            .GreaterThan(0)
            .WithMessage("Hãy chọn lớp muốn chuyển em sang.");

        ReasonRules.ApplyRequired(this, request => request.Reason, "Vui lòng nhập lý do chuyển lớp.");
    }
}
