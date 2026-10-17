using Anthill.Application.DTOs.Exams;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>Luật hình dạng của yêu cầu "soạn tự động" — chỉ kiểm số, việc rút câu thuộc về service.</summary>
public sealed class ComposeQuestionsRequestValidator : AbstractValidator<ComposeQuestionsRequest>
{
    /// <summary>Số lớp tối đa trong bộ lọc "câu lớp đã nhận"; đủ cho một giáo viên dạy cả khối.</summary>
    public const int MaxGroupFilters = 50;

    public ComposeQuestionsRequestValidator()
    {
        RuleFor(request => request.Easy)
            .InclusiveBetween(0, SaveExamRequestValidator.MaxTotalQuestions)
            .WithMessage("Số câu Dễ không hợp lệ.");

        RuleFor(request => request.Medium)
            .InclusiveBetween(0, SaveExamRequestValidator.MaxTotalQuestions)
            .WithMessage("Số câu Trung bình không hợp lệ.");

        RuleFor(request => request.Hard)
            .InclusiveBetween(0, SaveExamRequestValidator.MaxTotalQuestions)
            .WithMessage("Số câu Khó không hợp lệ.");

        // Cộng sau khi từng số đã nằm trong trần, nên không tràn.
        RuleFor(request => request)
            .Must(request => request.Easy + request.Medium + request.Hard >= 1)
            .WithName(nameof(ComposeQuestionsRequest.Easy))
            .WithMessage("Hãy nhập số câu cần soạn ít nhất ở một mức độ.")
            .Must(request => request.Easy + request.Medium + request.Hard <= SaveExamRequestValidator.MaxTotalQuestions)
            .WithMessage($"Một lần soạn tối đa {SaveExamRequestValidator.MaxTotalQuestions} câu.");

        RuleFor(request => request.Topic)
            .MaximumLength(SaveExamRequestValidator.MaxTopicLength)
            .WithMessage($"Tên chủ đề không được dài quá {SaveExamRequestValidator.MaxTopicLength} ký tự.");

        RuleFor(request => request.ExcludeQuestionIds)
            .Must(ids => ids!.Count <= SaveExamRequestValidator.MaxTotalQuestions)
            .WithMessage("Danh sách câu loại trừ quá dài.")
            .When(request => request.ExcludeQuestionIds is not null);

        RuleFor(request => request.ExcludeAssignedToGroupIds)
            .Must(ids => ids!.Count <= MaxGroupFilters)
            .WithMessage($"Chỉ lọc được tối đa {MaxGroupFilters} lớp một lần.")
            .When(request => request.ExcludeAssignedToGroupIds is not null);
    }
}
