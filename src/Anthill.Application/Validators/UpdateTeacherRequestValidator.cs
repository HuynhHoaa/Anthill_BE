using Anthill.Application.DTOs.Teachers;
using FluentValidation;

namespace Anthill.Application.Validators;

public sealed class UpdateTeacherRequestValidator : AbstractValidator<UpdateTeacherRequest>
{
    public UpdateTeacherRequestValidator()
    {
        StudentFieldRules.ApplyFullName(this, request => request.FullName);
        StudentFieldRules.ApplyEmail(this, request => request.Email);
        StudentFieldRules.ApplyRequiredPhone(this, request => request.Phone);

        RuleFor(request => request.SubjectId)
            .GreaterThan(0)
            .WithMessage("Hãy chọn môn giảng dạy.");
    }
}
