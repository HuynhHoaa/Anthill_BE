namespace Anthill.Application.DTOs.StudentGroups;

/// <summary>Thân request của <c>PUT /api/student-groups/{id}/teacher</c>: đổi giáo viên phụ trách lớp.</summary>
public sealed record ChangeTeacherRequest
{
    public int TeacherId { get; init; }

    /// <summary>
    /// Lý do đổi giáo viên (10–512 ký tự), lưu vào lịch sử của lớp. <b>Bắt buộc</b> khi lớp đang có giáo viên khác; không cần
    /// khi lớp chưa có ai phụ trách (lớp cũ) hoặc khi chọn lại đúng người đang phụ trách.
    /// </summary>
    public string? Reason { get; init; }
}
