namespace Anthill.Application.DTOs.Teachers;

/// <summary>Thân request của <c>PATCH /api/teachers/{id}/status</c>.</summary>
public sealed record UpdateTeacherStatusRequest
{
    /// <summary><c>"active"</c> hoặc <c>"locked"</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Bắt buộc (10–512 ký tự) khi khoá; mở khoá thì không cần.</summary>
    public string? Reason { get; init; }
}
