using EduQuiz.Application.DTOs.Auth;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Mappings;

/// <summary>
/// Ánh xạ thủ công <see cref="User"/> sang DTO. Không dùng AutoMapper: mapping viết tay đọc
/// được bằng mắt, và ở đây điều đó quan trọng — chính chỗ này là nơi quyết định cột nào
/// <b>không</b> rời server.
/// </summary>
public static class UserMappingExtensions
{
    /// <summary>
    /// Hồ sơ người dùng cho frontend.
    /// <para>
    /// Tên nhóm và tên môn đọc từ navigation property; truyền
    /// <paramref name="groupName"/>/<paramref name="subjectName"/> khi gọi từ chỗ không nạp kèm
    /// hai navigation đó, để tránh lazy loading âm thầm sinh truy vấn N+1.
    /// </para>
    /// <para>
    /// <b>PasswordHash và SecurityStamp không có mặt ở đây, và sẽ không bao giờ có.</b>
    /// </para>
    /// </summary>
    public static UserProfileDto ToProfileDto(this User user, string? groupName = null, string? subjectName = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserProfileDto
        {
            Id = user.Id,
            Role = user.Role.ToRoleString(),
            FullName = user.FullName,
            Email = user.Email,
            Username = user.Username,
            Code = user.Code,
            Group = groupName ?? user.Group?.Name,
            GroupId = user.GroupId,
            Phone = user.Phone,
            School = user.School,
            Subject = subjectName ?? user.Subject?.Name,
            AvatarColor = user.AvatarColor,
            JoinedAt = user.CreatedAt.ToUtc()
        };
    }

    /// <summary>
    /// Tên vai trò dạng chữ thường cho frontend: <c>"teacher" | "student" | "admin"</c>.
    /// Frontend khai báo union đúng ba giá trị đó và <c>RequireRole</c> so sánh trực tiếp,
    /// nên trả về <c>"Teacher"</c> theo tên enum .NET là đá văng người dùng khỏi mọi khu vực.
    /// </summary>
    /// <remarks>
    /// Khác với claim <c>role</c> trong JWT: claim giữ nguyên tên enum để khớp
    /// <c>SystemRoles</c> và <c>[Authorize(Roles = ...)]</c>. Hai chỗ này cố ý khác nhau.
    /// </remarks>
    public static string ToRoleString(this UserRole role) => role switch
    {
        UserRole.Admin => "admin",
        UserRole.Teacher => "teacher",
        UserRole.Student => "student",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Vai trò không hợp lệ.")
    };

    /// <summary>
    /// Đọc ngược tên vai trò do client gửi. Chỉ dùng để <b>đối chiếu</b> với vai trò thật lấy từ
    /// database (§3 quy tắc ⑤) — không bao giờ dùng để cấp quyền.
    /// </summary>
    public static bool TryParseRole(string? value, out UserRole role)
    {
        role = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        return Enum.TryParse(value.Trim(), ignoreCase: true, out role)
            && Enum.IsDefined(role);
    }

    /// <summary>
    /// Trạng thái tài khoản dạng chữ thường: <c>"active" | "locked"</c>, khớp union
    /// <c>StudentStatus</c> của frontend.
    /// </summary>
    public static string ToStatusString(this UserStatus status) => status switch
    {
        UserStatus.Active => "active",
        UserStatus.Locked => "locked",
        UserStatus.PendingDeletion => "pendingDeletion",
        UserStatus.Deleted => "deleted",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái không hợp lệ.")
    };
}
