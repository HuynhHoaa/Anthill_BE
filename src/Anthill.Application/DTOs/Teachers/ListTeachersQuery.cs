using Anthill.Application.Common;

namespace Anthill.Application.DTOs.Teachers;

/// <summary>Tham số của <c>GET /api/teachers</c>.</summary>
public sealed class ListTeachersQuery : PaginationParams
{
    /// <summary>Tìm theo họ tên (không dấu), email hoặc số điện thoại.</summary>
    public string? Search { get; set; }

    /// <summary><c>"active"</c>, <c>"locked"</c> hoặc <c>"all"</c>/bỏ trống.</summary>
    public string? Status { get; set; }

    public int? SubjectId { get; set; }
}
