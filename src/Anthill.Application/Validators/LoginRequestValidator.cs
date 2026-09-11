using Anthill.Application.DTOs.Auth;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Kiểm tra hình dạng của request đăng nhập — chỉ vậy thôi.
/// </summary>
/// <remarks>
/// Cố ý <b>không</b> validate độ dài hay độ mạnh của mật khẩu ở đây: mật khẩu cũ trong database
/// có thể ngắn hơn quy tắc hiện hành, mà chặn ngay ở cổng thì người dùng hợp lệ không vào nổi.
/// Cũng <b>không</b> kiểm <c>Role</c>: §3 quy tắc ⑤ nói rõ trường đó không được tham gia quyết
/// định gì, nên nó cũng không có lý do làm request thất bại.
/// </remarks>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Username)
            .Must(username => !string.IsNullOrWhiteSpace(username))
            .WithMessage("Vui lòng nhập tên đăng nhập.");

        RuleFor(request => request.Password)
            .Must(password => !string.IsNullOrWhiteSpace(password))
            .WithMessage("Vui lòng nhập mật khẩu.");
    }
}
