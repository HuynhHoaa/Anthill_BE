namespace Anthill.Application.DTOs.StudentGroups;

/// <summary>
/// Thân request của <c>POST /api/student-groups/{id}/lock</c> — khoá một lớp.
/// </summary>
/// <remarks>
/// Lý do bắt buộc (10–512 ký tự sau khi cắt khoảng trắng hai đầu) và được lưu vào lịch sử của lớp, cùng khuôn với các thao
/// tác tổ chức khác (khoá tài khoản, gỡ khỏi lớp, đổi giáo viên). Mở khoá không cần lý do.
/// </remarks>
public sealed record LockGroupRequest
{
    public required string Reason { get; init; }
}
