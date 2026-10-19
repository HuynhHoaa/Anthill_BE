using Anthill.Application.DTOs.Students;
using FluentValidation;

namespace Anthill.Application.Validators;

public sealed class RequestDeletionRequestValidator : AbstractValidator<RequestDeletionRequest>
{
    /// <summary>Đủ dài để loại "xoá", "ok", "abc" mà vẫn không cản người có việc thật.</summary>
    public const int MinimumReasonLength = ReasonRules.MinimumLength;

    public RequestDeletionRequestValidator()
    {
        // Cùng bộ luật với lý do khoá, gỡ khỏi lớp và chuyển lớp (ReasonRules).
        ReasonRules.ApplyRequired(this, request => request.Reason, "Vui lòng nhập lý do xoá tài khoản.");
    }
}
