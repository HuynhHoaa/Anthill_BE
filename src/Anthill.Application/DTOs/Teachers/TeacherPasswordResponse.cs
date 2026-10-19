namespace Anthill.Application.DTOs.Teachers;

/// <summary>
/// Kết quả cấp lại mật khẩu cho giáo viên. Mật khẩu ở dạng đọc được <b>đúng một lần</b>: database chỉ giữ chuỗi băm.
/// </summary>
public sealed record TeacherPasswordResponse
{
    public required int TeacherId { get; init; }

    public required string Password { get; init; }

    public required bool MustChangePassword { get; init; }
}
