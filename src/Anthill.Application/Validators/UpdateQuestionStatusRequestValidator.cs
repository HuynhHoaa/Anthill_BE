using Anthill.Application.DTOs.Questions;
using Anthill.Application.Mappings;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Trạng thái ở đây <b>bắt buộc</b>, khác với <c>Status</c> tuỳ chọn của
/// <see cref="SaveQuestionRequestValidator"/>: mục đích duy nhất của endpoint này là đổi trạng
/// thái, nên gửi thiếu là gửi một request rỗng nghĩa.
/// </summary>
public sealed class UpdateQuestionStatusRequestValidator : AbstractValidator<UpdateQuestionStatusRequest>
{
    public UpdateQuestionStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(status => QuestionMappingExtensions.TryParseQuestionStatus(status, out _))
            .WithMessage("Trạng thái câu hỏi chỉ nhận giá trị 'active' hoặc 'hidden'.");
    }
}
