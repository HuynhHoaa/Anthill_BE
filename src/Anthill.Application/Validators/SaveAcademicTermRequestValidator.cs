using Anthill.Application.DTOs.Terms;
using FluentValidation;

namespace Anthill.Application.Validators;

public sealed class SaveAcademicTermRequestValidator : AbstractValidator<SaveAcademicTermRequest>
{
    public const int MaxNameLength = 100;

    public SaveAcademicTermRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Tên kỳ học không được để trống.")
            .Must(name => (name ?? string.Empty).Trim().Length <= MaxNameLength)
            .WithMessage($"Tên kỳ học tối đa {MaxNameLength} ký tự.");

        RuleFor(request => request.EndsOn)
            .Must((request, endsOn) => endsOn is null || request.StartsOn is null || endsOn >= request.StartsOn)
            .WithMessage("Ngày kết thúc phải cùng hoặc sau ngày bắt đầu.");
    }
}
