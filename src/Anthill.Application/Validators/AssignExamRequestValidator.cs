using Anthill.Application.DTOs.ExamAssignments;
using FluentValidation;

namespace Anthill.Application.Validators;

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
            .Must(request => request.StudentIds is { Count: > 0 }
                             || request.GroupIds is { Count: > 0 }
                             || request.Classes is { Count: > 0 })
            .WithName(nameof(AssignExamRequest.StudentIds))
            .WithMessage("Hãy chọn ít nhất một học sinh hoặc một nhóm lớp.");

        // Một lớp chỉ được nói tới một lần. Nằm ở cả GroupIds lẫn Classes, hay hai lần trong Classes với hai
        // khung giờ, là hai câu trả lời cho cùng một câu hỏi "lớp này làm lúc nào" — chọn lặng lẽ một bên
        // là giao sai giờ cho cả lớp mà không ai biết.
        RuleFor(request => request)
            .Must(request => AllGroupIds(request).Count == AllGroupIds(request).Distinct().Count())
            .WithName(nameof(AssignExamRequest.Classes))
            .WithMessage("Mỗi lớp chỉ được chọn một lần trong một lần giao, kèm đúng một khung giờ.");

        RuleFor(request => request)
            .Must(request => AllGroupIds(request).Count <= MaxGroupsPerRequest)
            .WithName(nameof(AssignExamRequest.Classes))
            .WithMessage($"Mỗi lần chỉ chọn được tối đa {MaxGroupsPerRequest} nhóm lớp.");

        // NotNull đứng riêng trước ChildRules: bộ kiểm con bỏ qua phần tử null, nên "classes": [null] sẽ lọt
        // tới service và thành NullReferenceException.
        RuleForEach(request => request.Classes)
            .NotNull()
            .WithMessage("Danh sách lớp chứa một mục rỗng.")
            .ChildRules(target =>
            {
                target.RuleFor(item => item.GroupId)
                    .GreaterThan(0)
                    .WithMessage("Danh sách lớp chứa mã không hợp lệ.");

                target.RuleFor(item => item)
                    .Must(item => item.OpenAt.HasValue == item.CloseAt.HasValue)
                    .WithName(nameof(AssignClassTarget.OpenAt))
                    .WithMessage("Đặt giờ riêng cho lớp thì phải có đủ cả giờ mở và giờ đóng.");

                target.RuleFor(item => item)
                    .Must(item => item.OpenAt < item.CloseAt)
                    .WithName(nameof(AssignClassTarget.CloseAt))
                    .WithMessage("Giờ đóng bài của lớp phải sau giờ mở bài.")
                    .When(item => item.OpenAt.HasValue && item.CloseAt.HasValue);
            })
            .When(request => request.Classes is not null);

        // Hai mốc đi cùng cặp. Gửi một nửa là trạng thái không ai có ý định tạo ra, và im lặng
        // ghép nửa gửi lên với nửa lấy từ đề sẽ sinh ra một khung giờ chẳng giống ý ai cả. Thời lượng
        // so với khung giờ được kiểm ở ExamAssignmentService vì cần DurationMinutes của đề.
        RuleFor(request => request)
            .Must(request => request.OpenAt.HasValue == request.CloseAt.HasValue)
            .WithName(nameof(AssignExamRequest.OpenAt))
            .WithMessage("Đặt giờ riêng thì phải có đủ cả giờ mở và giờ đóng.");

        RuleFor(request => request)
            .Must(request => request.OpenAt < request.CloseAt)
            .WithName(nameof(AssignExamRequest.CloseAt))
            .WithMessage("Giờ đóng bài phải sau giờ mở bài.")
            .When(request => request.OpenAt.HasValue && request.CloseAt.HasValue);

        RuleFor(request => request.StudentIds)
            .Must(ids => ids!.Count <= MaxTargetsPerRequest)
            .WithMessage($"Mỗi lần chỉ chọn được tối đa {MaxTargetsPerRequest} học sinh.")
            .When(request => request.StudentIds is { Count: > 0 });

        RuleFor(request => request.StudentIds)
            .Must(ids => ids!.All(id => id > 0))
            .WithMessage("Danh sách học sinh chứa mã không hợp lệ.")
            .When(request => request.StudentIds is { Count: > 0 });

        RuleFor(request => request.GroupIds)
            .Must(ids => ids!.All(id => id > 0))
            .WithMessage("Danh sách nhóm lớp chứa mã không hợp lệ.")
            .When(request => request.GroupIds is { Count: > 0 });
    }

    /// <summary>Mọi mã lớp của request, gộp hai danh sách và giữ nguyên các lần lặp để còn đếm được.</summary>
    private static List<int> AllGroupIds(AssignExamRequest request) =>
        (request.GroupIds ?? [])
            .Concat((request.Classes ?? []).Where(target => target is not null).Select(target => target.GroupId))
            .ToList();
}
