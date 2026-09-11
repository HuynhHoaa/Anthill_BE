using Anthill.Application.DTOs.Students;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Cùng bộ quy tắc với <see cref="CreateStudentRequestValidator"/>, trừ mật khẩu —
/// <see cref="UpdateStudentRequest"/> cố ý không có trường đó (§4.3).
/// </summary>
public sealed class UpdateStudentRequestValidator : AbstractValidator<UpdateStudentRequest>
{
    public UpdateStudentRequestValidator()
    {
        StudentFieldRules.ApplyFullName(this, request => request.FullName);
        StudentFieldRules.ApplyEmail(this, request => request.Email);
        StudentFieldRules.ApplyUsername(this, request => request.Username);
        StudentFieldRules.ApplyCode(this, request => request.Code);
        StudentFieldRules.ApplyOptionalStatus(this, request => request.Status);

        RuleFor(request => request.GroupId)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Nhóm lớp không hợp lệ.")
            .When(request => request.GroupId.HasValue);
    }
}
