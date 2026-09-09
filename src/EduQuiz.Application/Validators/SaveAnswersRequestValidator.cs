using EduQuiz.Application.DTOs.ExamAttempts;
using FluentValidation;

namespace EduQuiz.Application.Validators;

/// <summary>Kiểm hình dạng gói lưu nháp.</summary>
/// <remarks>
/// Phần "đáp án này có thuộc câu hỏi đó không" <b>không</b> nằm ở đây: câu trả lời chỉ có trong bản
/// đề đã sinh, nên phép kiểm đó thuộc về service và trả 422 chứ không phải 400 (§5).
/// </remarks>
public sealed class SaveAnswersRequestValidator : AbstractValidator<SaveAnswersRequest>
{
    public SaveAnswersRequestValidator()
    {
        // Bắt đầu từ 1 chứ không phải 0: LastAppliedSeq của một lượt mới bằng 0, nên một gói mang
        // clientSeq = 0 sẽ bị chính bộ lọc chống ghi đè ngược thứ tự loại bỏ trong im lặng — client
        // quên gửi trường này sẽ tưởng mình đang lưu bài suốt buổi trong khi không có gì được ghi.
        RuleFor(request => request.ClientSeq)
            .GreaterThan(0)
            .WithMessage("Thiếu số thứ tự gói tin của phiên làm bài.");

        AnswerSelectionRules.ApplyTo(this, request => request.Answers, nameof(SaveAnswersRequest.Answers));
    }
}
