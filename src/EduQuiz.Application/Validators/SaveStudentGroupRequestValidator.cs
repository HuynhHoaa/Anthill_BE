using EduQuiz.Application.DTOs.StudentGroups;
using FluentValidation;

namespace EduQuiz.Application.Validators;

/// <summary>
/// Quy tắc lấy từ <c>groupService.validatePayload</c> của frontend, cộng giới hạn độ dài của
/// <c>StudentGroupConfiguration</c>.
/// </summary>
/// <remarks>
/// Phần "tên nhóm đã tồn tại" không nằm ở đây mà ở <c>StudentGroupService</c>: nó cần truy vấn
/// database, phạm vi so trùng là <b>trong nhóm của cùng một giáo viên</b> (thứ chỉ biết được từ
/// JWT), và kết quả phải là <c>409 Conflict</c> chứ không phải 400.
/// </remarks>
public sealed class SaveStudentGroupRequestValidator : AbstractValidator<SaveStudentGroupRequest>
{
    public const int MinNameLength = 2;
    public const int MaxNameLength = 128;
    public const int MaxDescriptionLength = 512;

    public SaveStudentGroupRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Tên nhóm lớp không được để trống.");

        RuleFor(request => request.Name)
            .Must(name => name!.Trim().Length >= MinNameLength)
            .WithMessage($"Tên nhóm lớp phải có ít nhất {MinNameLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.Name));

        RuleFor(request => request.Name)
            .Must(name => name!.Trim().Length <= MaxNameLength)
            .WithMessage($"Tên nhóm lớp không được vượt quá {MaxNameLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.Name));

        // Mô tả là tuỳ chọn: chuỗi rỗng nghĩa là xoá trắng, chỉ kiểm khi thật sự có nhập.
        RuleFor(request => request.Description)
            .Must(description => description!.Trim().Length <= MaxDescriptionLength)
            .WithMessage($"Mô tả không được vượt quá {MaxDescriptionLength} ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.Description));
    }
}
