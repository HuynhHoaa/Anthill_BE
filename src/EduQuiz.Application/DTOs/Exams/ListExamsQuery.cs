using EduQuiz.Application.Common;

namespace EduQuiz.Application.DTOs.Exams;

/// <summary>
/// Tham số của <c>GET /api/exams</c>. Kế thừa <see cref="PaginationParams"/> nên <c>page</c> và
/// <c>pageSize</c> được kẹp ngay lúc model binder gán giá trị (§5).
/// </summary>
public sealed class ListExamsQuery : PaginationParams
{
    /// <summary>Từ khoá tìm trên tiêu đề, mô tả và chủ đề. Bỏ dấu, không phân biệt hoa thường.</summary>
    public string? Search { get; set; }

    /// <summary>
    /// <c>"draft"</c>, <c>"scheduled"</c>, <c>"open"</c>, <c>"closed"</c>, hoặc <c>"all"</c>/bỏ trống.
    /// </summary>
    /// <remarks>
    /// Lọc theo vòng đời phải chạy <b>trong</b> truy vấn SQL, không phải lọc sau khi phân trang:
    /// lọc sau nghĩa là trang 1 có ba dòng, trang 2 có bảy dòng, và tổng số trang thì sai. Vì ba
    /// trong bốn giá trị là dẫn xuất từ mốc thời gian, repository nhận thêm một mốc <c>asOfUtc</c>
    /// để so — đọc <c>DateTime.UtcNow</c> rải rác giữa các dòng của cùng một truy vấn thì hai bản
    /// ghi có thể được đánh giá theo hai thời điểm khác nhau.
    /// </remarks>
    public string? Lifecycle { get; set; }
}
