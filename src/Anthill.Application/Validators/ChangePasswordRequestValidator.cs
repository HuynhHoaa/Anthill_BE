using Anthill.Application.DTOs.Auth;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Quy tắc lấy từ §5 mục Tài khoản, khớp thông điệp mà <c>authService.changePassword</c>
/// của frontend đang hiển thị.
/// </summary>
/// <remarks>
/// Mật khẩu <b>không</b> bị trim: khoảng trắng đầu và cuối là ký tự hợp lệ trong mật khẩu,
/// cắt đi là âm thầm đổi thứ người dùng đã gõ. Trim chỉ áp dụng cho email, tên và mã.
/// <para>
/// Validator này chỉ xét hình dạng. Việc <b>xác minh</b> mật khẩu hiện tại có đúng hay không
/// là của <c>IAuthService</c> — nó cần đọc database nên không thuộc về tầng validate.
/// </para>
/// </remarks>
public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword)
            .Must(password => !string.IsNullOrWhiteSpace(password))
            .WithMessage("Vui lòng nhập mật khẩu hiện tại.");

        RuleFor(request => request.NewPassword)
            .Must(password => !string.IsNullOrEmpty(password))
            .WithMessage("Vui lòng nhập mật khẩu mới.");

        RuleFor(request => request.NewPassword)
            .Must(password => password!.Length >= ValidationPatterns.MinPasswordLength)
            .WithMessage($"Mật khẩu mới phải có ít nhất {ValidationPatterns.MinPasswordLength} ký tự.")
            .When(request => !string.IsNullOrEmpty(request.NewPassword));

        RuleFor(request => request.NewPassword)
            .NotEqual(request => request.CurrentPassword)
            .WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.")
            .When(request => !string.IsNullOrEmpty(request.NewPassword));
    }
}
