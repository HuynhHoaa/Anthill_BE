using EduQuiz.Application.DTOs.ExamAttempts;
using FluentValidation;

namespace EduQuiz.Application.Validators;

/// <summary>Kiểm hình dạng request nộp bài.</summary>
/// <remarks>
/// <c>clientSeq</c> ở đây là tuỳ chọn, khác với lưu nháp: nộp bài xảy ra đúng một lần nên không có
/// gói nào để tới sai thứ tự. Nó chỉ có mặt để phần câu trả lời gửi kèm đi qua đúng bộ lọc mà các
/// gói lưu nháp đang đi qua.
/// </remarks>
public sealed class SubmitAttemptRequestValidator : AbstractValidator<SubmitAttemptRequest>
{
    public SubmitAttemptRequestValidator()
    {
        RuleFor(request => request.ClientSeq!.Value)
            .GreaterThan(0)
            .WithName(nameof(SubmitAttemptRequest.ClientSeq))
            .WithMessage("Số thứ tự gói tin không hợp lệ.")
            .When(request => request.ClientSeq.HasValue);

        AnswerSelectionRules.ApplyTo(this, request => request.Answers, nameof(SubmitAttemptRequest.Answers));
    }
}
