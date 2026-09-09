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
            CreatedAt = user.CreatedAt.ToUtc(),
            CompletedExams = completedExams,
            AverageScore = averageScore
        };
    }

    /// <summary>
    /// Đọc ngược chuỗi trạng thái do client gửi: <c>"active" | "locked"</c>.
    /// </summary>
    /// <remarks>
    /// Cặp đôi của <see cref="UserMappingExtensions.ToStatusString"/>. Dùng
    /// <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> nên <c>"Active"</c> và
    /// <c>"active"</c> đều qua, còn <c>Enum.IsDefined</c> chặn trường hợp client gửi số:
    /// <c>"7"</c> parse thành công thành một <see cref="UserStatus"/> không tồn tại.
    /// </remarks>
    public static bool TryParseStatus(string? value, out UserStatus status)
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
    /// Đọc chuỗi trạng thái ở ô <b>lọc</b>: khác <see cref="TryParseStatus"/> ở chỗ chấp nhận thêm
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

        if (!TryParseStatus(value, out var parsed))
        {
            return false;
        }

        status = parsed;
        return true;
    }
}
