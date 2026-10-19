using Anthill.Application.Common;
using Anthill.Application.DTOs.Accounts;
using Anthill.Application.DTOs.Students;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Mappings;

/// <summary>
/// Ánh xạ <see cref="User"/> sang <see cref="StudentDto"/>. Cùng lý do với
/// <see cref="UserMappingExtensions"/>: viết tay để nhìn bằng mắt là biết cột nào rời server.
/// </summary>
public static class StudentMappingExtensions
{
    /// <summary>
    /// Dựng DTO học sinh.
    /// </summary>
    /// <param name="user">Tài khoản có <c>Role = Student</c>.</param>
    /// <param name="completedExams">Số lượt đã nộp hoặc đã chấm, đọc từ <c>IExamAttemptRepository</c>.</param>
    /// <param name="averageScore">Trung bình điểm thang 10 trên mọi bài đã giao (bài chưa làm tính 0).</param>
    /// <param name="startedExams">Số lượt làm bài ở bất kỳ trạng thái nào; khác 0 là em đã bắt đầu làm bài.</param>
    /// <param name="groups">
    /// Các lớp của em khi chỗ gọi không nạp kèm navigation <c>Groups</c> (đường ghi nạp tài khoản trần). Bỏ trống thì đọc từ
    /// navigation; không có cả hai thì mảng rỗng, nghĩa là "chưa có lớp nào".
    /// </param>
    /// <param name="visibleGroupIds">
    /// Chỉ giữ những lớp có id trong tập này — phạm vi của giáo viên (lớp mình dạy). <c>null</c> là không giới hạn (quản trị viên).
    /// </param>
    /// <remarks>
    /// Hai con số thống kê là tham số bắt buộc chứ không có giá trị mặc định: chúng phải được lấy
    /// theo lô cho cả trang danh sách (<c>GetStatsByStudentsAsync</c>). Cho mặc định 0 nghĩa là
    /// quên truyền cũng biên dịch được, và màn hình lặng lẽ hiện 0 điểm cho mọi học sinh.
    /// </remarks>
    public static StudentDto ToStudentDto(
        this User user,
        int completedExams,
        decimal averageScore,
        int startedExams,
        IReadOnlyCollection<StudentGroup>? groups = null,
        IReadOnlySet<int>? visibleGroupIds = null,
        bool includePhone = false,
        int assignedExams = 0)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new StudentDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Username = user.Username,

            // Số điện thoại chỉ đi tới quản trị viên: giáo viên không có lý do cần nó để dạy hay giao bài.
            Phone = includePhone ? user.Phone : null,

            // Mã học sinh là NOT NULL về mặt nghiệp vụ nhưng nullable trong bảng dùng chung ba vai
            // trò. Rơi về chuỗi rỗng thay vì ném lỗi: một bản ghi lỗi dữ liệu không nên làm hỏng cả
            // trang danh sách, và ô trống trên màn hình đủ để người dùng thấy có gì đó bất thường.
            Code = user.Code ?? string.Empty,

            Groups = ToGroupRefs((IEnumerable<StudentGroup>?)groups ?? user.Groups, visibleGroupIds),
            Status = user.Status.ToStatusString(),

            // Ba trường của quy trình xoá. Vắng mặt với tài khoản bình thường nhờ cấu hình bỏ qua
            // giá trị null toàn cục, nên không làm phình response của danh sách học sinh.
            DeletionReason = user.DeletionReason,
            DeletionRequestedAt = user.DeletionRequestedAt?.ToUtc(),
            DeletionScheduledAt = user.Status == UserStatus.PendingDeletion
                ? user.DeletionRequestedAt?.AddDays(StudentAnonymizer.RetentionDays).ToUtc()
                : null,
            CreatedAt = user.CreatedAt.ToUtc(),
            CompletedExams = completedExams,
            AverageScore = averageScore,
            AssignedExams = assignedExams,
            HasStartedExams = startedExams > 0
        };
    }

    /// <summary>Các lớp của em theo tên, đã lọc theo phạm vi người xem nếu có.</summary>
    private static List<StudentGroupRefDto> ToGroupRefs(
        IEnumerable<StudentGroup> groups,
        IReadOnlySet<int>? visibleGroupIds) =>
        groups
            .Where(group => visibleGroupIds is null || visibleGroupIds.Contains(group.Id))
            .OrderBy(group => group.Name, StringComparer.CurrentCulture)
            .Select(group => new StudentGroupRefDto { Id = group.Id, Name = group.Name, IsLocked = group.IsLocked })
            .ToList();

    /// <summary>Các lớp của một học sinh dạng tóm tắt, cho chỗ chỉ có entity lớp trong tay.</summary>
    public static IReadOnlyList<StudentGroupRefDto> ToGroupRefDtos(this IEnumerable<StudentGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return ToGroupRefs(groups, visibleGroupIds: null);
    }

    /// <summary>
    /// Đọc trạng thái mà quản trị viên được phép <b>đặt trực tiếp</b>: chỉ <c>active</c> và <c>locked</c>.
    /// </summary>
    /// <remarks>
    /// Hai trạng thái còn lại của quy trình xoá — chờ xoá và đã xoá — có lối đi riêng
    /// (<c>request-deletion</c>, <c>restore</c>), vì mỗi lối phải làm thêm việc: ghi lý do, xoay
    /// SecurityStamp, xoá dấu vết khi khôi phục. Trước đây hàm này nhận cả bốn, nên PATCH status
    /// đặt thẳng <c>deleted</c> được — tài khoản mang nhãn đã xoá trong khi họ tên, email, số điện
    /// thoại vẫn còn nguyên, vì bước ẩn danh hoá không bao giờ chạy.
    /// </remarks>
    /// <summary>
    /// Một dòng lịch sử. <paramref name="change"/> phải được nạp kèm <c>Actor</c> thì mới có tên người thực hiện.
    /// </summary>
    public static AccountChangeDto ToDto(this AccountChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var kind = change.Kind.ToString();

        return new AccountChangeDto
        {
            Id = change.Id,
            Kind = char.ToLowerInvariant(kind[0]) + kind[1..],
            Reason = change.Reason,
            FromGroup = change.FromGroupName,
            ToGroup = change.ToGroupName,
            ActorName = change.Actor?.FullName,
            CreatedAt = change.CreatedAt.ToUtc()
        };
    }

    public static bool TryParseSettableStatus(string? value, out UserStatus status)
    {
        if (TryParseAnyStatus(value, out status) && status is UserStatus.Active or UserStatus.Locked)
        {
            return true;
        }

        status = default;
        return false;
    }

    /// <summary>Đọc bất kỳ trạng thái nào. Chỉ ô lọc được dùng, vì ở đó phải tìm ra cả tài khoản chờ xoá.</summary>
    private static bool TryParseAnyStatus(string? value, out UserStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out status) && Enum.IsDefined(status);
    }

    /// <summary>Giá trị frontend gửi khi ô lọc đang để "tất cả".</summary>
    public const string AllFilterValue = "all";

    /// <summary>
    /// Đọc chuỗi trạng thái ở ô <b>lọc</b>: khác <see cref="TryParseSettableStatus"/> ở chỗ chấp nhận thêm
    /// <c>"all"</c> và chuỗi rỗng, cả hai nghĩa là không lọc.
    /// </summary>
    /// <returns>False khi giá trị không đọc được — chỗ gọi trả lỗi thay vì âm thầm bỏ lọc.</returns>
    public static bool TryParseStatusFilter(string? value, out UserStatus? status)
    {
        status = null;

        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value.Trim(), AllFilterValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!TryParseAnyStatus(value, out var parsed))
        {
            return false;
        }

        status = parsed;
        return true;
    }
}
