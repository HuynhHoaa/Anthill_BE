namespace Anthill.Application.DTOs.ExamAttempts;

/// <summary>
/// Tham số của <c>GET /api/my/exams</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Không có</b> <c>studentId</c>, và đó là chủ ý: danh tính suy từ JWT (§3 quy tắc ④). Thêm
/// trường đó vào đây là mở đường cho một em đọc danh sách bài của em khác chỉ bằng cách đổi một số
/// trên thanh địa chỉ.
/// </para>
/// <para>
/// Cũng không phân trang và không có ô tìm kiếm — §4.7 không khai hai thứ đó cho endpoint này. Một
/// học sinh có nhiều nhất vài chục bài trong một học kỳ và màn hình của các em hiện tất cả trong
/// một danh sách cuộn, nên phần lọc còn lại làm ngay trên máy người dùng là đủ.
/// </para>
/// </remarks>
public sealed class ListMyExamsQuery
{
    /// <summary>
    /// <c>"assigned"</c> · <c>"inprogress"</c> · <c>"submitted"</c> · <c>"graded"</c> ·
    /// <c>"expired"</c>, hoặc <c>"all"</c>/bỏ trống.
    /// </summary>
    /// <remarks>
    /// Lọc trong bộ nhớ chứ không trong SQL, khác với danh sách đề của giáo viên. Trạng thái ở đây
    /// là dữ liệu dẫn xuất từ lượt làm bài <i>và</i> lịch đề, mà tập dữ liệu chỉ vài chục dòng —
    /// dịch phép suy đó thành câu SQL để đổi lấy đúng một lần quét bảng nhỏ là không đáng.
    /// </remarks>
    public string? Status { get; set; }
}
