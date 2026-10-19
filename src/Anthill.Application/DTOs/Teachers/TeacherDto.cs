namespace Anthill.Application.DTOs.Teachers;

/// <summary>
/// Một tài khoản giáo viên, đúng như quản trị viên quản lý — <c>/api/teachers</c>.
/// </summary>
/// <remarks>
/// Số điện thoại có mặt vì endpoint này chỉ quản trị viên gọi được. <see cref="Groups"/> chỉ có ở chi tiết một giáo viên
/// (<c>GET /api/teachers/{id}</c>); danh sách chỉ mang <see cref="GroupCount"/> để khỏi truy vấn theo từng người.
/// </remarks>
public sealed record TeacherDto
{
    public required int Id { get; init; }

    public required string FullName { get; init; }

    /// <summary>Email cũng là tên đăng nhập của giáo viên.</summary>
    public required string Email { get; init; }

    public string? Phone { get; init; }

    public int? SubjectId { get; init; }

    /// <summary>Tên môn; chuỗi rỗng khi tài khoản chưa được gán môn.</summary>
    public required string Subject { get; init; }

    /// <summary><c>"active"</c> hoặc <c>"locked"</c>.</summary>
    public required string Status { get; init; }

    public required string AvatarColor { get; init; }

    public string? AvatarUrl { get; init; }

    /// <summary>Còn dùng mật khẩu do quản trị viên cấp, chưa đổi lần nào.</summary>
    public required bool MustChangePassword { get; init; }

    /// <summary>Số lớp đang được phân công phụ trách (kể cả lớp còn là bản nháp).</summary>
    public required int GroupCount { get; init; }

    public required DateTime CreatedAt { get; init; }

    /// <summary>Các lớp đang phụ trách. Chỉ có ở chi tiết một giáo viên.</summary>
    public IReadOnlyList<TeacherGroupDto>? Groups { get; init; }
}

/// <summary>Một lớp giáo viên đang phụ trách, tóm tắt.</summary>
public sealed record TeacherGroupDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required int StudentCount { get; init; }

    public required bool IsPublished { get; init; }
}
