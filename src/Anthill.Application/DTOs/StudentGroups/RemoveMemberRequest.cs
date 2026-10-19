namespace Anthill.Application.DTOs.StudentGroups;

/// <summary>
/// Thân request của <c>DELETE /api/student-groups/{id}/students/{studentId}</c> — gỡ một học sinh khỏi lớp.
/// </summary>
/// <remarks>
/// Lý do bắt buộc: em bị gỡ khỏi lớp là một quyết định mà người xem sau này cần hiểu (nghỉ học, chuyển trường,
/// xếp nhầm). Lý do được lưu vào lịch sử của học sinh, cùng người thực hiện và thời điểm.
/// </remarks>
public sealed record RemoveMemberRequest
{
    /// <summary>10–512 ký tự sau khi cắt khoảng trắng hai đầu.</summary>
    public required string Reason { get; init; }
}
