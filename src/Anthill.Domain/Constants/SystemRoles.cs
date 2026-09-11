namespace Anthill.Domain.Constants;

/// <summary>
/// Tên vai trò dạng chuỗi, khớp từng ký tự với <see cref="Enums.UserRole"/> vì claim "role"
/// trong JWT mang đúng tên enum. Dùng cho <c>[Authorize(Roles = ...)]</c> ở tầng Api.
/// </summary>
public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    public const string AdminOrTeacher = $"{Admin},{Teacher}";
    public const string All = $"{Admin},{Teacher},{Student}";
}
