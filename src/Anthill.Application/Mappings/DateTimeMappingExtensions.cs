namespace Anthill.Application.Mappings;

/// <summary>
/// Chốt <c>DateTimeKind</c> trước khi serialize.
/// </summary>
/// <remarks>
/// Lý do tồn tại: <c>System.Text.Json</c> chỉ thêm hậu tố <c>Z</c> khi <c>Kind = Utc</c>.
/// Một <see cref="DateTime"/> có <c>Kind = Unspecified</c> lọt ra ngoài sẽ thành chuỗi ISO
/// <b>không có timezone</b>, và trình duyệt hiểu chuỗi đó theo giờ máy người dùng — lệch đúng
/// bằng chênh lệch múi giờ. Với một hệ thống mà hạn nộp bài tính bằng phút (§3 quy tắc ③),
/// đó là lỗi nghiêm trọng chứ không phải lỗi hiển thị.
/// </remarks>
public static class DateTimeMappingExtensions
{
    public static DateTime ToUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static DateTime? ToUtc(this DateTime? value) => value?.ToUtc();
}
