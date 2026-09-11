using Anthill.Application.DTOs.Exams;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Kiểm hai mốc thời gian của <c>PATCH /api/exams/{id}/schedule</c>.
/// </summary>
/// <remarks>
/// Không kiểm được "thời lượng có lọt trong cửa sổ không" ở đây, vì <c>durationMinutes</c> nằm
/// trên đề chứ không nằm trong request — phép đó thuộc về <c>ExamService</c>, nơi đọc được đề.
/// </remarks>
public sealed class UpdateScheduleRequestValidator : AbstractValidator<UpdateScheduleRequest>
{
    public UpdateScheduleRequestValidator()
    {
        RuleFor(request => request.CloseAt)
            .GreaterThan(request => request.OpenAt)
            .WithMessage("Thời gian đóng bài phải sau thời gian mở bài.");
    }
}
