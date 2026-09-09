using EduQuiz.Application.DTOs.ExamAssignments;
using FluentValidation;

namespace EduQuiz.Application.Validators;

/// <summary>Kiểm hình dạng request giao bài.</summary>
/// <remarks>
/// Chỉ chặn hai thứ nhìn vào request là biết: không chọn ai cả, và chọn quá nhiều. Mọi phép kiểm
/// còn lại — id có thật không, học sinh có thuộc lớp mình không, tài khoản có bị khoá không —
/// đều cần đọc database nên thuộc về service.
/// </remarks>
public sealed class AssignExamRequestValidator : AbstractValidator<AssignExamRequest>
{
    /// <summary>
    /// Trần cho một lần gọi. Một khối lớp phổ thông hiếm khi quá vài trăm em, mà mỗi em khi bật
    /// <c>randomizePerStudent</c> lại kéo theo một bản đề đầy đủ trong cùng transaction.
    /// </summary>
    public const int MaxTargetsPerRequest = 500;

    public AssignExamRequestValidator()
    {
        RuleFor(request => request)
            .Must(request => request.StudentIds.Count > 0 || request.GroupIds.Count > 0)
            .WithName(nameof(AssignExamRequest.StudentIds))
            .WithMessage("Hãy chọn ít nhất một học sinh hoặc một nhóm lớp.");

        RuleFor(request => request.StudentIds.Count)
            .LessThanOrEqualTo(MaxTargetsPerRequest)
            .WithMessage($"Mỗi lần chỉ giao được tối đa {MaxTargetsPerRequest} học sinh.");

        RuleFor(request => request.GroupIds.Count)
            .LessThanOrEqualTo(MaxTargetsPerRequest)
            .WithMessage($"Mỗi lần chỉ chọn được tối đa {MaxTargetsPerRequest} nhóm lớp.");
    }
}
