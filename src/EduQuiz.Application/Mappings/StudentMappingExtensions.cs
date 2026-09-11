using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.Students;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Mappings;

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
    /// <param name="averageScore">Trung bình điểm các lượt đã chấm.</param>
    /// <param name="groupName">
    /// Tên nhóm khi chỗ gọi không nạp kèm navigation <c>Group</c>. Bỏ trống thì đọc từ navigation;
    /// không có cả hai thì rơi về nhãn "Chưa phân nhóm".
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
        string? groupName = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new StudentDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Username = user.Username,

            // Mã học sinh là NOT NULL về mặt nghiệp vụ nhưng nullable trong bảng dùng chung ba vai
            // trò. Rơi về chuỗi rỗng thay vì ném lỗi: một bản ghi lỗi dữ liệu không nên làm hỏng cả
            // trang danh sách, và ô trống trên màn hình đủ để người dùng thấy có gì đó bất thường.
            Code = user.Code ?? string.Empty,

            Group = StudentGroupDefaults.ToClientGroupName(groupName ?? user.Group?.Name),
            GroupId = StudentGroupDefaults.ToClientGroupId(user.GroupId),
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
            AverageScore = averageScore
        };
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
