using Anthill.Domain.Constants;
using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>
/// Ai được đổi ảnh đại diện và đường dẫn ảnh nào hợp lệ (<c>PUT /api/auth/me/avatar</c>).
/// </summary>
/// <remarks>
/// <para>
/// Quản trị viên và giáo viên tự đổi ảnh của mình. Học sinh thì không: hồ sơ học sinh do nhà trường quản lý (xem
/// <see cref="ProfileEditPolicy"/>), và <c>/api/uploads/images</c> cũng chỉ mở cho hai vai trò kia — cho học sinh tải ảnh
/// lên là mở một chỗ ghi file công khai cho mọi tài khoản.
/// </para>
/// <para>
/// Đường dẫn phải là ảnh do chính <c>/api/uploads/images</c> cấp (tương đối, bắt đầu bằng
/// <see cref="FileStorageConstants.PublicRequestPathPrefix"/>). Một URL tuyệt đối tới máy chủ khác sẽ bị trình duyệt của mọi
/// người xem tải về, lộ địa chỉ và thời điểm họ mở trang cho bên thứ ba.
/// </para>
/// </remarks>
public static class AvatarPolicy
{
    /// <summary>Cột <c>Users.AvatarUrl</c> dài 512 ký tự.</summary>
    public const int MaxUrlLength = 512;

    public static bool CanChange(UserRole role) => role is UserRole.Admin or UserRole.Teacher;

    /// <summary>
    /// Chuẩn hoá đường dẫn gửi lên: trống thì thành null (gỡ ảnh), còn lại cắt khoảng trắng hai đầu.
    /// </summary>
    public static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Thông điệp lỗi nếu đường dẫn (đã chuẩn hoá) không hợp lệ; null khi hợp lệ hoặc là lệnh gỡ ảnh.</summary>
    public static string? Validate(string? normalizedUrl)
    {
        if (normalizedUrl is null)
        {
            return null;
        }

        if (normalizedUrl.Length > MaxUrlLength)
        {
            return $"Đường dẫn ảnh không được vượt quá {MaxUrlLength} ký tự.";
        }

        if (!normalizedUrl.StartsWith($"{FileStorageConstants.PublicRequestPathPrefix}/", StringComparison.Ordinal)
            || normalizedUrl.Contains("..", StringComparison.Ordinal))
        {
            return "Ảnh đại diện phải là ảnh đã tải lên qua /api/uploads/images.";
        }

        return null;
    }
}
