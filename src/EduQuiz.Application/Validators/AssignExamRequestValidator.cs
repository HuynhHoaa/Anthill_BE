using EduQuiz.Application.DTOs.ExamAssignments;
using FluentValidation;

namespace EduQuiz.Application.Validators;

/// <summary>Kiểm hình dạng request giao bài.</summary>
/// <remarks>
/// <para>
/// Chỉ chặn những thứ nhìn vào request là biết. Mọi phép kiểm còn lại — id có thật không, học sinh
/// có thuộc lớp mình không, tài khoản có bị khoá không — đều cần đọc database nên thuộc về service.
/// </para>
/// <para>
/// Hai danh sách phải được <b>null-guard</b>. DTO khai giá trị mặc định là mảng rỗng, nhưng
/// <c>System.Text.Json</c> ghi đè giá trị đó bằng <c>null</c> khi client gửi thẳng
/// <c>"studentIds": null</c> — và một quy tắc đọc <c>.Count</c> trên đó sẽ ném
/// <see cref="NullReferenceException"/>, biến một request sai định dạng thành lỗi 500. Cùng khuôn
/// với <see cref="AddMembersRequestValidator"/>.
/// </para>
/// </remarks>
public sealed class AssignExamRequestValidator : AbstractValidator<AssignExamRequest>
{
    /// <summary>
    /// Trần cho <b>tổng số học sinh</b> của một lần gọi, tính sau khi đã bung các nhóm ra.
    /// </summary>
    /// <remarks>
    /// Đặt trần trên từng danh sách riêng là hàng rào hở: 500 nhóm mỗi nhóm 40 em vẫn lọt qua, rồi
    /// thành 20.000 assignment cộng 20.000 bản đề trong một transaction. Vì số học sinh thật chỉ
    /// biết sau khi đọc database, phép kiểm cuối cùng nằm ở service; ở đây chặn phần thô.
    /// </remarks>
    public const int MaxTargetsPerRequest = 500;

    /// <summary>Trần số nhóm gửi kèm. Một giáo viên hiếm khi phụ trách quá chừng này lớp.</summary>
    public const int MaxGroupsPerRequest = 50;

    public AssignExamRequestValidator()
    {
        RuleFor(request => request)
            .Must(request => request.StudentIds is { Count: > 0 } || request.GroupIds is { Count: > 0 })
            .WithName(nameof(AssignExamRequest.StudentIds))
            .WithMessage("Hãy chọn ít nhất một học sinh hoặc một nhóm lớp.");

        RuleFor(request => request.StudentIds)
            .Must(ids => ids!.Count <= MaxTargetsPerRequest)
            .WithMessage($"Mỗi lần chỉ chọn được tối đa {MaxTargetsPerRequest} học sinh.")
            .When(request => request.StudentIds is { Count: > 0 });

        RuleFor(request => request.StudentIds)
            .Must(ids => ids!.All(id => id > 0))
            .WithMessage("Danh sách học sinh chứa mã không hợp lệ.")
            .When(request => request.StudentIds is { Count: > 0 });

        RuleFor(request => request.GroupIds)
            .Must(ids => ids!.Count <= MaxGroupsPerRequest)
            .WithMessage($"Mỗi lần chỉ chọn được tối đa {MaxGroupsPerRequest} nhóm lớp.")
            .When(request => request.GroupIds is { Count: > 0 });

        RuleFor(request => request.GroupIds)
            .Must(ids => ids!.All(id => id > 0))
            .WithMessage("Danh sách nhóm lớp chứa mã không hợp lệ.")
            .When(request => request.GroupIds is { Count: > 0 });
    }
}
