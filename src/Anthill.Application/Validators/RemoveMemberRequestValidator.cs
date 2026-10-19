using Anthill.Application.DTOs.StudentGroups;
using FluentValidation;

namespace Anthill.Application.Validators;

public sealed class RemoveMemberRequestValidator : AbstractValidator<RemoveMemberRequest>
{
    public RemoveMemberRequestValidator()
    {
        ReasonRules.ApplyRequired(this, request => request.Reason, "Vui lòng nhập lý do gỡ học sinh khỏi lớp.");
    }
}
