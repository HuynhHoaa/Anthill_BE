using EduQuiz.Application.Mappings;

namespace EduQuiz.Application.Common;

/// <summary>
/// Hạn giờ của một lượt làm bài, tính bằng <b>đồng hồ server</b> (§3 quy tắc ③).
/// </summary>
/// <remarks>
/// <para>
/// Tách thành hàm thuần vì đây là phép tính duy nhất quyết định một bài còn được sửa hay không, và
/// nó bị gọi từ bốn chỗ khác nhau: lúc bắt đầu, lúc lưu nháp, lúc nộp và trong background job.
/// Bốn bản sao của cùng một phép so là bốn cơ hội để một bản viết <c>&gt;=</c> còn ba bản kia viết
/// <c>&gt;</c>.
/// </para>
/// <para>
/// Không có tham số nào lấy từ client. Đồng hồ máy học sinh chỉ dùng để vẽ con số đếm ngược trên
/// màn hình; mọi quyết định đều so với <c>DateTime.UtcNow</c> và cột <c>ExpiresAt</c> trong database.
/// </para>
/// </remarks>
public static class AttemptDeadline
{
    /// <summary>
    /// Thời điểm hết giờ của một lượt vừa bắt đầu.
    /// </summary>
    /// <remarks>
    /// <b>Kẹp vào giờ đóng đề</b> là phần dễ bỏ sót. §5 đã bắt thời lượng không được dài hơn cửa sổ
    /// mở đề, nhưng luật đó chỉ nói về <i>cả cửa sổ</i>: một em bấm bắt đầu khi đề chỉ còn mở năm
    /// phút vẫn sẽ được cấp trọn bốn mươi lăm phút nếu không kẹp, và làm bài tiếp ba mươi lăm phút
    /// sau khi cửa đã đóng cho cả lớp.
    /// </remarks>
    public static DateTime Resolve(DateTime startedAtUtc, int durationMinutes, DateTime closeAtUtc)
    {
        var started = startedAtUtc.ToUtc();
        var closeAt = closeAtUtc.ToUtc();
        var natural = started.AddMinutes(durationMinutes);

        return natural > closeAt ? closeAt : natural;
    }

    /// <summary>
    /// Đã quá hạn hay chưa. So <b>chặt</b>: đúng giây hết giờ vẫn còn được tính là kịp.
    /// </summary>
    /// <remarks>
    /// Cùng quy ước với <c>ExamMappingExtensions.ResolveLifecycle</c> ở mốc <c>closeAt</c>. Hai chỗ
    /// hiểu khác nhau về giây cuối cùng nghĩa là có một khoảnh khắc mà đề vẫn "đang mở" nhưng bài
    /// thì đã "hết giờ".
    /// </remarks>
    public static bool IsExpired(DateTime expiresAtUtc, DateTime nowUtc) => nowUtc.ToUtc() > expiresAtUtc.ToUtc();

    /// <summary>
    /// Số giây còn lại để client vẽ đồng hồ đếm ngược, không bao giờ âm.
    /// </summary>
    /// <remarks>
    /// Làm tròn <b>lên</b>: còn 0,4 giây mà trả về 0 thì màn hình hiện "hết giờ" trong khi server
    /// vẫn còn nhận bài — người dùng đọc được sự mâu thuẫn đó ngay.
    /// </remarks>
    public static int RemainingSeconds(DateTime expiresAtUtc, DateTime nowUtc)
    {
        var remaining = (expiresAtUtc.ToUtc() - nowUtc.ToUtc()).TotalSeconds;

        return remaining <= 0 ? 0 : (int)Math.Ceiling(remaining);
    }
}
