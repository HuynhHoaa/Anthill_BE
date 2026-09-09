using EduQuiz.Application.Common;

namespace EduQuiz.Application.DTOs.Results;

/// <summary>
/// Bộ lọc lịch sử làm bài của chính học sinh — <c>GET /api/my/results</c>.
/// </summary>
/// <remarks>
/// Cố ý <b>không có</b> <c>studentId</c>. Danh tính lấy từ token (§3 quy tắc ④); để trường đó ở
/// đây là mở đường cho một em đọc lịch sử của em khác bằng cách sửa một số trên thanh địa chỉ.
/// </remarks>
public sealed class ListMyResultsQuery : PaginationParams
{
    /// <summary>Tìm trên tên đề và chủ đề.</summary>
    public string? Search { get; set; }
}
