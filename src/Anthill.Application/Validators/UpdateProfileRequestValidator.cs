using Anthill.Application.DTOs.Auth;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Quy tắc lấy từ §5 mục Tài khoản và từ chính <c>authService.updateProfile</c> của frontend.
/// Mọi chuỗi được xét <b>sau khi trim</b>, vì §5 yêu cầu trim trước khi lưu và trước khi so trùng.
/// </summary>
/// <remarks>
/// Từng điều kiện nằm ở một <c>RuleFor</c> riêng chứ không nối đuôi nhau: <c>When</c> của
/// FluentValidation áp cho <b>toàn bộ</b> chuỗi rule trước nó, nên gộp "không được để trống" và
/// "đúng định dạng" vào một chuỗi rồi thêm <c>When(có nhập)</c> sẽ vô hiệu hoá luôn vế đầu —
/// gửi email rỗng lại lọt qua.
/// </remarks>
public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    /// <summary>Khớp cột <c>Users.FullName</c> và <c>StudentFieldRules.MaxFullNameLength</c>: quản trị viên tạo được tên dài tới đây.</summary>
    private const int MaxFullNameLength = 128;
    private const int MaxSchoolLength = 200;

    /// <summary>Khớp cột <c>Users.Email</c> và <c>StudentFieldRules.MaxEmailLength</c>.</summary>
    private const int MaxEmailLength = 256;

    public UpdateProfileRequestValidator()
    {
        RuleFor(request => request.FullName)
            .Must(fullName => !string.IsNullOrWhiteSpace(fullName))
            .WithMessage("Họ và tên không được để trống.");

        RuleFor(request => request.FullName)
            .Must(fullName => fullName!.Trim().Length <= MaxFullNameLength)
            .WithMessage($"Họ và tên không được vượt quá {MaxFullNameLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.FullName));

        RuleFor(request => request.Email)
            .Must(email => !string.IsNullOrWhiteSpace(email))
            .WithMessage("Email không được để trống.");

        RuleFor(request => request.Email)
            .Must(email => ValidationPatterns.Email.IsMatch(email!.Trim()))
            .WithMessage("Email không đúng định dạng.")
            .When(request => !string.IsNullOrWhiteSpace(request.Email));

        RuleFor(request => request.Email)
            .Must(email => email!.Trim().Length <= MaxEmailLength)
            .WithMessage($"Email không được vượt quá {MaxEmailLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.Email));

        // Số điện thoại là tuỳ chọn: chuỗi rỗng nghĩa là xoá trắng, chỉ kiểm khi thật sự có nhập.
        RuleFor(request => request.Phone)
            .Must(phone => ValidationPatterns.Phone.IsMatch(phone!.Trim()))
            .WithMessage("Số điện thoại không hợp lệ.")
            .When(request => !string.IsNullOrWhiteSpace(request.Phone));

        RuleFor(request => request.School)
            .Must(school => school!.Trim().Length <= MaxSchoolLength)
            .WithMessage($"Tên trường không được vượt quá {MaxSchoolLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.School));
    }
}
