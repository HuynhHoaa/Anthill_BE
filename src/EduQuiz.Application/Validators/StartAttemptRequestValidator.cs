using EduQuiz.Application.DTOs.ExamAttempts;
using FluentValidation;

namespace EduQuiz.Application.Validators;

/// <summary>Kiểm hình dạng request bắt đầu làm bài.</summary>
/// <remarks>
/// Chỉ chặn được đúng một thứ nhìn vào request là biết: id phải là số dương. Câu hỏi thật —
/// assignment này có thuộc về em không, đề đã mở chưa, em đã làm chưa — đều cần đọc database nên
/// thuộc về service.
/// </remarks>
public sealed class StartAttemptRequestValidator : AbstractValidator<StartAttemptRequest>
{
    public StartAttemptRequestValidator()
    {
        RuleFor(request => request.AssignmentId)
            .GreaterThan(0)
            .WithMessage("Thiếu mã bài kiểm tra được giao.");
    }
}
