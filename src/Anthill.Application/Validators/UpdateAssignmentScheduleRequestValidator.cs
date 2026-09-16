using Anthill.Application.DTOs.ExamAssignments;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>Kiểm cặp giờ khi dời giờ cho một em.</summary>
/// <remarks>
/// Thời lượng so với khung giờ được kiểm ở service, vì <c>durationMinutes</c> nằm trên đề chứ không nằm
/// trong request — cùng cách chia việc với <see cref="AssignExamRequestValidator"/>.
/// </remarks>
public sealed class UpdateAssignmentScheduleRequestValidator : AbstractValidator<UpdateAssignmentScheduleRequest>
{
    public UpdateAssignmentScheduleRequestValidator()
    {
        RuleFor(request => request)
            .Must(request => request.OpenAt.HasValue == request.CloseAt.HasValue)
            .WithName(nameof(UpdateAssignmentScheduleRequest.OpenAt))
            .WithMessage("Đặt giờ riêng thì phải có đủ cả giờ mở và giờ đóng; bỏ trống cả hai để theo lịch chung của đề.");

        RuleFor(request => request)
            .Must(request => request.OpenAt < request.CloseAt)
            .WithName(nameof(UpdateAssignmentScheduleRequest.CloseAt))
            .WithMessage("Giờ đóng bài phải sau giờ mở bài.")
            .When(request => request.OpenAt.HasValue && request.CloseAt.HasValue);
    }
}
