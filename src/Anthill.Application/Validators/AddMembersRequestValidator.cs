using Anthill.Application.DTOs.StudentGroups;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Kiểm hình dạng mảng id. Việc id nào tồn tại, id nào đã ở sẵn trong nhóm là chuyện của
/// <c>StudentGroupService</c> — những trường hợp đó <b>không phải lỗi</b> mà là các con số trong
/// <see cref="AddMembersResponse"/>.
/// </summary>
public sealed class AddMembersRequestValidator : AbstractValidator<AddMembersRequest>
{
    /// <summary>
    /// Trần bằng <c>PaginationParams.MaxPageSize</c>: màn hình chọn thành viên lấy danh sách qua
    /// endpoint có phân trang, nên không có cách nào chọn hợp lệ quá con số này trong một lần.
    /// </summary>
    public const int MaxStudentsPerCall = 200;

    public AddMembersRequestValidator()
    {
        RuleFor(request => request.StudentIds)
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("Hãy chọn ít nhất một học sinh.");

        RuleFor(request => request.StudentIds)
            .Must(ids => ids!.Count <= MaxStudentsPerCall)
            .WithMessage($"Mỗi lần chỉ thêm được tối đa {MaxStudentsPerCall} học sinh.")
            .When(request => request.StudentIds is { Count: > 0 });

        RuleFor(request => request.StudentIds)
            .Must(ids => ids!.All(id => id > 0))
            .WithMessage("Danh sách học sinh chứa mã không hợp lệ.")
            .When(request => request.StudentIds is { Count: > 0 });
    }
}
