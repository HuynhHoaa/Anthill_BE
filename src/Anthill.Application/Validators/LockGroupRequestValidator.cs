using Anthill.Application.DTOs.StudentGroups;
using FluentValidation;

namespace Anthill.Application.Validators;

public sealed class LockGroupRequestValidator : AbstractValidator<LockGroupRequest>
{
    public LockGroupRequestValidator()
    {
        ReasonRules.ApplyRequired(this, request => request.Reason, "Vui lòng nhập lý do khoá lớp.");
    }
}
