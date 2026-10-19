namespace Anthill.Application.DTOs.StudentGroups;

/// <summary>Một dòng lịch sử của lớp — <c>GET /api/student-groups/{id}/history</c>, chỉ quản trị viên đọc.</summary>
public sealed record GroupChangeDto
{
    public required int Id { get; init; }

    /// <summary><c>created</c> hoặc <c>teacherChanged</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Lý do quản trị viên nhập; vắng mặt khi lớp ra đời hoặc khi lớp chưa có giáo viên để "đổi".</summary>
    public string? Reason { get; init; }

    /// <summary>Tên giáo viên cũ đúng như lúc xảy ra.</summary>
    public string? FromTeacher { get; init; }

    public string? ToTeacher { get; init; }

    /// <summary>Tên quản trị viên thực hiện; vắng mặt khi tài khoản đó không còn.</summary>
    public string? ActorName { get; init; }

    /// <summary>ISO 8601 UTC (§1).</summary>
    public required DateTime CreatedAt { get; init; }
}
