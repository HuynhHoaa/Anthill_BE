using Anthill.Application.DTOs.Teachers;
using Anthill.Application.Mappings;
using Anthill.Domain.Enums;
using FluentValidation;

namespace Anthill.Application.Validators;

public sealed class UpdateTeacherStatusRequestValidator : AbstractValidator<UpdateTeacherStatusRequest>
{
    public UpdateTeacherStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(status => StudentMappingExtensions.TryParseSettableStatus(status, out _))
            .WithMessage("Trạng thái tài khoản chỉ nhận giá trị 'active' hoặc 'locked'.");

        // Khoá làm một giáo viên mất quyền đăng nhập ngay lập tức nên bắt buộc nói lý do, cùng luật với khoá học sinh. Mở
        // khoá thì không.
        When(
            request => StudentMappingExtensions.TryParseSettableStatus(request.Status, out var status)
                       && status == UserStatus.Locked,
            () => ReasonRules.ApplyRequired(this, request => request.Reason, "Vui lòng nhập lý do khoá tài khoản."));
    }
}
