namespace Anthill.Application.DTOs.Auth;

/// <summary>
/// Hồ sơ người đang đăng nhập. Khớp từng trường với interface <c>UserProfile</c> của frontend
/// (<c>src/types/index.ts</c>) để tầng service FE thay ruột mà không trang nào phải sửa.
/// </summary>
/// <remarks>
/// <b>Không</b> chứa <c>PasswordHash</c> hay <c>SecurityStamp</c>, và sẽ không bao giờ chứa:
/// hai cột đó không có lý do rời server.
/// </remarks>
public sealed record UserProfileDto
{
    public required int Id { get; init; }

    /// <summary>
    /// Chữ thường: <c>"teacher" | "student" | "admin"</c>. Frontend khai báo union đúng như vậy
    /// và <c>RequireRole</c> so sánh trực tiếp — trả về <c>"Teacher"</c> theo tên enum .NET là
    /// đá văng người dùng khỏi mọi khu vực. Xem <c>UserMappingExtensions.ToRoleString</c>.
    /// </summary>
    public required string Role { get; init; }

    public required string FullName { get; init; }

    public required string Email { get; init; }

    public required string Username { get; init; }

    /// <summary>Mã học sinh. NULL với giáo viên và quản trị viên.</summary>
    public string? Code { get; init; }

    /// <summary>Tên nhóm lớp, dữ liệu JOIN nên <b>chỉ đọc</b>. Client gửi lên thì bỏ qua (§5).</summary>
    public string? Group { get; init; }

    public int? GroupId { get; init; }

    public string? Phone { get; init; }

    /// <summary>Tên môn của giáo viên, JOIN từ Subject. NULL với hai vai trò còn lại.</summary>
    public string? Subject { get; init; }

    public required string AvatarColor { get; init; }

    /// <summary>Đường dẫn ảnh đại diện đã tải lên; NULL khi chưa có (giao diện vẽ chữ cái đầu trên nền <c>AvatarColor</c>).</summary>
    public string? AvatarUrl { get; init; }

    /// <summary>ISO 8601 UTC khi serialize, đúng quy ước §1.</summary>
    public required DateTime JoinedAt { get; init; }
}
