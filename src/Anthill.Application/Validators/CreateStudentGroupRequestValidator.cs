using Anthill.Application.DTOs.StudentGroups;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Quy tắc của <c>POST /api/student-groups</c>: cùng luật tên và mô tả với đổi tên lớp
/// (<see cref="SaveStudentGroupRequestValidator"/>), cộng giáo viên phụ trách bắt buộc.
/// </summary>
public sealed class CreateStudentGroupRequestValidator : AbstractValidator<CreateStudentGroupRequest>
{
    public CreateStudentGroupRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Tên nhóm lớp không được để trống.");

        RuleFor(request => request.Name)
            .Must(name => name!.Trim().Length >= SaveStudentGroupRequestValidator.MinNameLength)
            .WithMessage($"Tên nhóm lớp phải có ít nhất {SaveStudentGroupRequestValidator.MinNameLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.Name));

        RuleFor(request => request.Name)
            .Must(name => name!.Trim().Length <= SaveStudentGroupRequestValidator.MaxNameLength)
            .WithMessage($"Tên nhóm lớp không được vượt quá {SaveStudentGroupRequestValidator.MaxNameLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.Name));

        RuleFor(request => request.Description)
            .Must(description => description!.Trim().Length <= SaveStudentGroupRequestValidator.MaxDescriptionLength)
            .WithMessage($"Mô tả không được vượt quá {SaveStudentGroupRequestValidator.MaxDescriptionLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.Description));

        RuleFor(request => request.TeacherId)
            .GreaterThan(0)
            .WithMessage("Hãy chọn giáo viên phụ trách lớp.");
    }
}
