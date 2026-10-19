namespace Anthill.Application.DTOs.Teachers;

/// <summary>Thân request của <c>PUT /api/teachers/{id}</c>. Không đổi trạng thái hay mật khẩu: hai việc đó có endpoint riêng.</summary>
public sealed record UpdateTeacherRequest
{
    public required string FullName { get; init; }

    public required string Email { get; init; }

    /// <summary>Bắt buộc, 9–15 chữ số: thiếu hay để trống trả 400 (giáo viên cũ chưa có số thì phải nhập số ở lần sửa kế tiếp).</summary>
    public string? Phone { get; init; }

    public int SubjectId { get; init; }
}
