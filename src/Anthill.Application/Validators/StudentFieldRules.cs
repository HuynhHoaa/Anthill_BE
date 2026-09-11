using Anthill.Application.Mappings;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Quy tắc dùng chung cho <see cref="CreateStudentRequestValidator"/> và
/// <see cref="UpdateStudentRequestValidator"/> — hai request có bốn trường định danh giống hệt
/// nhau, và chúng phải được kiểm giống hệt nhau.
/// </summary>
/// <remarks>
/// <para>
/// Tách ra đây thay vì chép đôi: chép đôi thì một ngày nào đó ô "tạo mới" chấp nhận username 2 ký
/// tự còn ô "sửa" thì không, và người dùng không có cách nào đoán được vì sao.
/// </para>
/// <para>
/// Giới hạn độ dài lấy đúng từ <c>UserConfiguration</c>. Không kiểm ở đây thì một họ tên 200 ký
/// tự đi hết đường xuống database rồi mới vỡ thành lỗi 500 — thay vì một câu tiếng Việt gắn đúng
/// vào ô nhập.
/// </para>
/// <para>
/// Từng điều kiện nằm ở một <c>RuleFor</c> riêng, cùng lý do đã ghi ở
/// <see cref="UpdateProfileRequestValidator"/>: <c>When</c> áp cho toàn bộ chuỗi rule đứng trước
/// nó, nên nối "không được trống" với "đúng định dạng" rồi thêm <c>When(có nhập)</c> sẽ vô hiệu
/// hoá luôn vế đầu.
/// </para>
/// </remarks>
internal static class StudentFieldRules
{
    public const int MaxFullNameLength = 128;
    public const int MaxEmailLength = 256;
    public const int MaxUsernameLength = 64;
    public const int MaxCodeLength = 32;

    /// <summary>Cùng ngưỡng với <c>studentService.validatePayload</c> của frontend.</summary>
    public const int MinUsernameLength = 3;

    public static void ApplyFullName<T>(AbstractValidator<T> validator, Func<T, string?> selector)
    {
        validator.RuleFor(request => selector(request))
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithName(StudentFieldNames.FullName)
            .WithMessage("Họ và tên không được để trống.");

        validator.RuleFor(request => selector(request))
            .Must(value => value!.Trim().Length <= MaxFullNameLength)
            .WithName(StudentFieldNames.FullName)
            .WithMessage($"Họ và tên không được vượt quá {MaxFullNameLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(selector(request)));
    }

    public static void ApplyEmail<T>(AbstractValidator<T> validator, Func<T, string?> selector)
    {
        validator.RuleFor(request => selector(request))
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithName(StudentFieldNames.Email)
            .WithMessage("Email không được để trống.");

        validator.RuleFor(request => selector(request))
            .Must(value => ValidationPatterns.Email.IsMatch(value!.Trim()))
            .WithName(StudentFieldNames.Email)
            .WithMessage("Email không đúng định dạng.")
            .When(request => !string.IsNullOrWhiteSpace(selector(request)));

        validator.RuleFor(request => selector(request))
            .Must(value => value!.Trim().Length <= MaxEmailLength)
            .WithName(StudentFieldNames.Email)
            .WithMessage($"Email không được vượt quá {MaxEmailLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(selector(request)));
    }

    public static void ApplyUsername<T>(AbstractValidator<T> validator, Func<T, string?> selector)
    {
        validator.RuleFor(request => selector(request))
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithName(StudentFieldNames.Username)
            .WithMessage("Tên đăng nhập không được để trống.");

        validator.RuleFor(request => selector(request))
            .Must(value => value!.Trim().Length >= MinUsernameLength)
            .WithName(StudentFieldNames.Username)
            .WithMessage($"Tên đăng nhập phải có ít nhất {MinUsernameLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(selector(request)));

        validator.RuleFor(request => selector(request))
            .Must(value => value!.Trim().Length <= MaxUsernameLength)
            .WithName(StudentFieldNames.Username)
            .WithMessage($"Tên đăng nhập không được vượt quá {MaxUsernameLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(selector(request)));
    }

    public static void ApplyCode<T>(AbstractValidator<T> validator, Func<T, string?> selector)
    {
        validator.RuleFor(request => selector(request))
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithName(StudentFieldNames.Code)
            .WithMessage("Mã học sinh không được để trống.");

        validator.RuleFor(request => selector(request))
            .Must(value => value!.Trim().Length <= MaxCodeLength)
            .WithName(StudentFieldNames.Code)
            .WithMessage($"Mã học sinh không được vượt quá {MaxCodeLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(selector(request)));
    }

    /// <summary>
    /// Trạng thái là tuỳ chọn ở cả hai request, nhưng giá trị lạ phải bị từ chối — bỏ qua âm thầm
    /// nghĩa là gửi <c>"khoa"</c> thì tài khoản vẫn mở, và không ai biết lệnh khoá đã rơi mất.
    /// </summary>
    public static void ApplyOptionalStatus<T>(AbstractValidator<T> validator, Func<T, string?> selector)
    {
        validator.RuleFor(request => selector(request))
            .Must(value => StudentMappingExtensions.TryParseSettableStatus(value, out _))
            .WithName(StudentFieldNames.Status)
            .WithMessage("Trạng thái tài khoản chỉ nhận giá trị 'active' hoặc 'locked'.")
            .When(request => !string.IsNullOrWhiteSpace(selector(request)));
    }

    /// <summary>
    /// Tên trường cho <c>WithName</c>. Các rule ở trên dùng biểu thức <c>selector(request)</c> nên
    /// FluentValidation không suy ra được tên thuộc tính; thiếu <c>WithName</c> thì khoá lỗi trả về
    /// là chuỗi rỗng và form ở frontend không biết gắn thông báo vào ô nào.
    /// </summary>
    private static class StudentFieldNames
    {
        public const string FullName = nameof(FullName);
        public const string Email = nameof(Email);
        public const string Username = nameof(Username);
        public const string Code = nameof(Code);
        public const string Status = nameof(Status);
    }
}
