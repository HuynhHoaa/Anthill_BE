using Anthill.Application.DTOs.Students;
using Anthill.Application.Mappings;
using Anthill.Domain.Enums;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Trạng thái ở đây <b>bắt buộc</b>, khác với <c>Status</c> tuỳ chọn của hai request kia: mục
/// đích duy nhất của endpoint này là đổi trạng thái, nên gửi thiếu là gửi một request rỗng nghĩa.
/// Khoá tài khoản còn bắt buộc kèm lý do.
/// </summary>
public sealed class UpdateStudentStatusRequestValidator : AbstractValidator<UpdateStudentStatusRequest>
{
    public UpdateStudentStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(status => StudentMappingExtensions.TryParseSettableStatus(status, out _))
            .WithMessage("Trạng thái tài khoản chỉ nhận giá trị 'active' hoặc 'locked'.");

        // Khoá tài khoản là việc làm một em mất quyền đăng nhập ngay lập tức, nên bắt buộc nói lý do. Mở khoá thì
        // không: không ai cần giải thích vì sao trả lại quyền đăng nhập.
        When(
            request => StudentMappingExtensions.TryParseSettableStatus(request.Status, out var status)
                       && status == UserStatus.Locked,
            () => ReasonRules.ApplyRequired(this, request => request.Reason, "Vui lòng nhập lý do khoá tài khoản."));
    }
}
