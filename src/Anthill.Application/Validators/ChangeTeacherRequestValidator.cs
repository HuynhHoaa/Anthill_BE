using Anthill.Application.DTOs.StudentGroups;
using FluentValidation;

namespace Anthill.Application.Validators;

public sealed class ChangeTeacherRequestValidator : AbstractValidator<ChangeTeacherRequest>
{
    public ChangeTeacherRequestValidator()
    {
        RuleFor(request => request.TeacherId)
            .GreaterThan(0)
            .WithMessage("Hãy chọn giáo viên phụ trách lớp.");

        ReasonRules.ApplyOptional(this, request => request.Reason);
    }
}
