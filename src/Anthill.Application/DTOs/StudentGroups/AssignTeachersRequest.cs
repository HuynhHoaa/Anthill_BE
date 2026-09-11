namespace Anthill.Application.DTOs.StudentGroups;

/// <summary>Thân request của <c>POST /api/student-groups/{id}/teachers</c>.</summary>
/// <remarks>
/// Nhận cả danh sách thay vì từng người một, cùng khuôn với việc thêm học sinh theo lô: phân công
/// một lúc bốn giáo viên bộ môn cho một lớp là thao tác thường gặp, và làm bốn lần request cho một
/// hành động là bắt người dùng trả giá cho lựa chọn thiết kế của mình.
/// </remarks>
public sealed record AssignTeachersRequest
{
    public required IReadOnlyList<int> TeacherIds { get; init; }
}
