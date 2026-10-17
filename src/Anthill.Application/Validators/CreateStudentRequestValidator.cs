using Anthill.Application.DTOs.Students;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Kiểm <b>hình dạng</b> dữ liệu tạo tài khoản học sinh. Phần "đã có ai dùng chưa" không nằm ở
/// đây mà ở <c>StudentService</c>: nó cần truy vấn database, và §5 yêu cầu trùng định danh trả
/// <c>409 Conflict</c> chứ không phải <c>400</c> — mà validator thì luôn trả 400.
/// </summary>
public sealed class CreateStudentRequestValidator : AbstractValidator<CreateStudentRequest>
{
    public CreateStudentRequestValidator()
    {
        StudentFieldRules.ApplyFullName(this, request => request.FullName);
        StudentFieldRules.ApplyEmail(this, request => request.Email);
        StudentFieldRules.ApplyUsername(this, request => request.Username);
        StudentFieldRules.ApplyCode(this, request => request.Code);
        StudentFieldRules.ApplyOptionalPhone(this, request => request.Phone);
        StudentFieldRules.ApplyOptionalStatus(this, request => request.Status);

        // Mật khẩu không trim, cùng lý do đã ghi ở AuthService.ChangePasswordAsync: khoảng trắng
        // đầu và cuối là ký tự hợp lệ, cắt đi là âm thầm đổi thứ người dùng đã gõ. Nhưng chuỗi chỉ
        // toàn khoảng trắng thì coi như trống: bộ băm từ chối nó, và để lọt xuống đó là lỗi 500.
        RuleFor(request => request.Password)
            .Must(password => !string.IsNullOrWhiteSpace(password))
            .WithMessage("Mật khẩu ban đầu không được để trống.");

        RuleFor(request => request.Password)
            .Must(password => password!.Length >= ValidationPatterns.MinPasswordLength)
            .WithMessage($"Mật khẩu phải có ít nhất {ValidationPatterns.MinPasswordLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.Password));

        // Số âm không phải "chưa phân nhóm" (đó là 0) mà là dữ liệu hỏng — chặn ngay để service
        // không phải phân biệt hai trường hợp.
        RuleFor(request => request.GroupId)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Nhóm lớp không hợp lệ.")
            .When(request => request.GroupId.HasValue);
    }
}
