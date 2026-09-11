using EduQuiz.Application.DTOs.Students;
using EduQuiz.Application.Mappings;
using FluentValidation;

namespace EduQuiz.Application.Validators;

/// <summary>
/// Trạng thái ở đây <b>bắt buộc</b>, khác với <c>Status</c> tuỳ chọn của hai request kia: mục
/// đích duy nhất của endpoint này là đổi trạng thái, nên gửi thiếu là gửi một request rỗng nghĩa.
/// </summary>
public sealed class UpdateStudentStatusRequestValidator : AbstractValidator<UpdateStudentStatusRequest>
{
    public UpdateStudentStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(status => StudentMappingExtensions.TryParseSettableStatus(status, out _))
            .WithMessage("Trạng thái tài khoản chỉ nhận giá trị 'active' hoặc 'locked'.");
    }
}
