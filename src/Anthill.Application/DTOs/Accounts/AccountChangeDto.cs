namespace Anthill.Application.DTOs.Accounts;

/// <summary>
/// Một dòng lịch sử của một tài khoản — <c>GET /api/students/{id}/history</c> và <c>GET /api/teachers/{id}/history</c>,
/// chỉ quản trị viên đọc.
/// </summary>
public sealed record AccountChangeDto
{
    public required int Id { get; init; }

    /// <summary>
    /// <c>locked</c>, <c>unlocked</c>, <c>deletionRequested</c>, <c>restored</c>, <c>addedToGroup</c>,
    /// <c>removedFromGroup</c> hoặc <c>movedGroup</c>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>Lý do quản trị viên nhập; vắng mặt với các loại không bắt buộc lý do.</summary>
    public string? Reason { get; init; }

    /// <summary>Tên lớp cũ đúng như lúc xảy ra, không phải tên hiện tại của lớp.</summary>
    public string? FromGroup { get; init; }

    public string? ToGroup { get; init; }

    /// <summary>Tên quản trị viên thực hiện; vắng mặt khi tài khoản đó không còn.</summary>
    public string? ActorName { get; init; }

    /// <summary>ISO 8601 UTC (§1).</summary>
    public required DateTime CreatedAt { get; init; }
}
