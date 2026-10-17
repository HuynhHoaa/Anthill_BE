using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>
/// Ai được tự sửa gì trong hồ sơ của chính mình (<c>PUT /api/auth/me</c>).
/// </summary>
/// <remarks>
/// <para>
/// Họ tên, email và số điện thoại là thông tin do <b>nhà trường</b> (quản trị viên) quản lý:
/// </para>
/// <list type="bullet">
/// <item><description>quản trị viên: sửa được cả ba;</description></item>
/// <item><description>giáo viên: chỉ sửa được họ tên — email và số điện thoại do quản trị viên đổi;</description></item>
/// <item><description>học sinh: không sửa được trường nào.</description></item>
/// </list>
/// <para>
/// Môn giảng dạy không nằm trong request nên không có đường nào để đổi. Form hồ sơ luôn gửi đủ ba trường, nên
/// gửi lại đúng giá trị đang có là hợp lệ; chỉ giá trị khác đi mới tính là "đổi".
/// </para>
/// </remarks>
public static class ProfileEditPolicy
{
    public const string FullNameField = "fullName";
    public const string EmailField = "email";
    public const string PhoneField = "phone";

    /// <summary>
    /// Các trường người dùng đang cố đổi mà không được phép. Rỗng nghĩa là request hợp lệ.
    /// </summary>
    /// <param name="role">Vai trò của chủ tài khoản.</param>
    /// <param name="currentFullName">Họ tên đang lưu.</param>
    /// <param name="currentEmail">Email đang lưu.</param>
    /// <param name="currentPhone">Số điện thoại đang lưu (có thể null).</param>
    /// <param name="fullName">Họ tên gửi lên, đã trim.</param>
    /// <param name="email">Email gửi lên, đã trim.</param>
    /// <param name="phone">Số điện thoại gửi lên, đã trim; null khi bỏ trống.</param>
    public static IReadOnlyList<string> LockedFieldsChanged(
        UserRole role,
        string currentFullName,
        string currentEmail,
        string? currentPhone,
        string fullName,
        string email,
        string? phone)
    {
        if (role == UserRole.Admin)
        {
            return [];
        }

        var locked = new List<string>();

        if (role == UserRole.Student
            && !string.Equals(fullName, currentFullName, StringComparison.Ordinal))
        {
            locked.Add(FullNameField);
        }

        if (!string.Equals(email, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            locked.Add(EmailField);
        }

        // Số điện thoại lưu đã trim và rỗng thành null, nên so hai bên theo cùng quy tắc.
        var normalizedCurrent = string.IsNullOrWhiteSpace(currentPhone) ? null : currentPhone.Trim();
        var normalizedRequested = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (!string.Equals(normalizedRequested, normalizedCurrent, StringComparison.Ordinal))
        {
            locked.Add(PhoneField);
        }

        return locked;
    }
}
