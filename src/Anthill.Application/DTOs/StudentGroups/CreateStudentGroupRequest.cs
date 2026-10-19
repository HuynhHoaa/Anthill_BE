namespace Anthill.Application.DTOs.StudentGroups;

/// <summary>
/// Thân request của <c>POST /api/student-groups</c>: lập một lớp mới.
/// </summary>
/// <remarks>
/// Khác <see cref="SaveStudentGroupRequest"/> (dùng cho đổi tên và mô tả) ở chỗ bắt buộc có giáo viên: mỗi lớp có đúng một
/// giáo viên phụ trách, chọn ngay lúc lập lớp. Không có <c>OwnerTeacherId</c> kiểu cũ — lớp thuộc về tổ chức, người lập
/// lớp lấy từ JWT.
/// </remarks>
public sealed record CreateStudentGroupRequest
{
    public required string Name { get; init; }

    /// <summary>Tuỳ chọn.</summary>
    public string? Description { get; init; }

    /// <summary>Giáo viên phụ trách lớp. Phải là tài khoản giáo viên đang hoạt động.</summary>
    public int TeacherId { get; init; }

    /// <summary>Kỳ học của lớp (không bắt buộc).</summary>
    public int? TermId { get; init; }
}
