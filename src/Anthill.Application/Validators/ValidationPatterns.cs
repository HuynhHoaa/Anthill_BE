using System.Text.RegularExpressions;

namespace Anthill.Application.Validators;

/// <summary>
/// Biểu thức chính quy dùng chung. Cố ý chép <b>đúng</b> mẫu của frontend
/// (<c>authService.updateProfile</c>, <c>ProfileInfoForm</c>) để một giá trị đã qua được ô nhập
/// thì cũng qua được server — hai lớp validate lệch nhau sinh ra lỗi không giải thích nổi
/// cho người dùng.
/// </summary>
public static class ValidationPatterns
{
    public const int MinPasswordLength = 6;

    /// <summary>Cùng mẫu với frontend: <c>^[^\s@]+@[^\s@]+\.[^\s@]+$</c>.</summary>
    public static readonly Regex Email = new(
        @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Cùng mẫu với frontend: <c>^[0-9\s.+-]{9,15}$</c>. Chỉ kiểm khi người dùng có nhập.</summary>
    public static readonly Regex Phone = new(
        @"^[0-9\s.+-]{9,15}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
