namespace EduQuiz.Application.Common;

/// <summary>
/// Màu nền cho ảnh đại diện chữ cái. Cột <c>User.AvatarColor</c> là NOT NULL nên tài khoản nào
/// cũng cần một giá trị ngay lúc tạo.
/// </summary>
/// <remarks>
/// <para>
/// Chọn theo <b>hàm băm của tên đăng nhập</b> chứ không phải ngẫu nhiên: cùng một học sinh thì
/// luôn ra cùng một màu, kể cả khi tài khoản bị xoá rồi tạo lại. Màu đổi mỗi lần xem là thứ người
/// dùng nhận ra ngay và đọc thành "hệ thống chạy sai".
/// </para>
/// <para>
/// Không dùng <c>string.GetHashCode()</c>: .NET gieo ngẫu nhiên hàm băm chuỗi theo từng tiến
/// trình, nên màu sẽ đổi sau mỗi lần khởi động lại server — mà dịch vụ miễn phí thì khởi động
/// lại thường xuyên. FNV-1a dưới đây cho cùng một kết quả ở mọi tiến trình.
/// </para>
/// </remarks>
public static class AvatarPalette
{
    /// <summary>
    /// Bảng màu lấy từ Tailwind, cùng gam với ba tài khoản demo trong <c>DbSeeder</c>. Đủ đậm để
    /// chữ trắng đặt lên vẫn đọc được.
    /// </summary>
    private static readonly string[] Colors =
    [
        "#0EA5E9", // sky-500
        "#6366F1", // indigo-500
        "#8B5CF6", // violet-500
        "#EC4899", // pink-500
        "#F97316", // orange-500
        "#10B981", // emerald-500
        "#0891B2", // cyan-600
        "#D946EF"  // fuchsia-500
    ];

    public static string ForKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Colors[0];
        }

        return Colors[(int)(Fnv1a(key.Trim()) % (uint)Colors.Length)];
    }

    /// <summary>FNV-1a 32 bit — ngắn, không phụ thuộc tiến trình, đủ tản đều cho tám ô màu.</summary>
    private static uint Fnv1a(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= prime;
        }

        return hash;
    }
}
