using Anthill.Application.Common;

namespace Anthill.Application.DTOs.Results;

/// <summary>
/// Bộ lọc chéo của bảng kết quả — <c>GET /api/results</c> và <c>GET /api/results/stats</c> dùng chung.
/// </summary>
/// <remarks>
/// Dùng chung một kiểu cho cả bảng lẫn bốn ô số liệu là có chủ đích: hai phần đó nằm trên cùng một
/// màn hình, nên chúng phải nói về cùng một tập dữ liệu.
/// </remarks>
public sealed class ListResultsQuery : PaginationParams
{
    /// <summary>Tìm trên tên học sinh, mã học sinh và tên đề. Bỏ dấu, không phân biệt hoa thường.</summary>
    public string? Search { get; set; }

    public int? ExamId { get; set; }

    public int? StudentId { get; set; }

    public int? GroupId { get; set; }

    /// <summary><c>"submitted"</c>, <c>"graded"</c>, <c>"inprogress"</c>, <c>"expired"</c>, hoặc <c>"all"</c>.</summary>
    public string? Status { get; set; }
}
