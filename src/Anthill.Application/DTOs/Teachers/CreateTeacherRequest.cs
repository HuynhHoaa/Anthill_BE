namespace Anthill.Application.DTOs.Teachers;

/// <summary>Thân request của <c>POST /api/teachers</c>.</summary>
/// <remarks>
/// Email là tên đăng nhập nên không có trường <c>username</c> riêng. Số điện thoại bắt buộc (nhà trường cần liên lạc được với giáo
/// viên). Môn bắt buộc: giáo viên chưa gán môn không soạn được
/// câu hỏi hay đề nào. Mật khẩu do quản trị viên cấp, ép đổi ở lần đăng nhập đầu.
/// </remarks>
public sealed record CreateTeacherRequest
{
    public required string FullName { get; init; }

    public required string Email { get; init; }

    /// <summary>Bắt buộc, 9–15 chữ số (cho phép dấu cách, +, -, ngoặc).</summary>
    public string? Phone { get; init; }

    public int SubjectId { get; init; }

    public required string Password { get; init; }
}
