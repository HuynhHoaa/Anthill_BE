using Anthill.Application.DTOs.Teachers;
using FluentValidation;

namespace Anthill.Application.Validators;

public sealed class CreateTeacherRequestValidator : AbstractValidator<CreateTeacherRequest>
{
    public CreateTeacherRequestValidator()
    {
        StudentFieldRules.ApplyFullName(this, request => request.FullName);
        StudentFieldRules.ApplyEmail(this, request => request.Email);
        StudentFieldRules.ApplyRequiredPhone(this, request => request.Phone);

        RuleFor(request => request.SubjectId)
            .GreaterThan(0)
            .WithMessage("Hãy chọn môn giảng dạy.");

        // Mật khẩu không trim, cùng lý do với tạo học sinh: khoảng trắng đầu cuối là ký tự hợp lệ. Chuỗi chỉ toàn khoảng
        // trắng thì coi như trống vì bộ băm từ chối nó.
        RuleFor(request => request.Password)
            .Must(password => !string.IsNullOrWhiteSpace(password))
            .WithMessage("Mật khẩu ban đầu không được để trống.");

        RuleFor(request => request.Password)
            .Must(password => password!.Length >= ValidationPatterns.MinPasswordLength)
            .WithMessage($"Mật khẩu phải có ít nhất {ValidationPatterns.MinPasswordLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.Password));
    }
}
