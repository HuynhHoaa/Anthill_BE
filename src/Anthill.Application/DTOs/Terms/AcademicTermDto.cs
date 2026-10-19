namespace Anthill.Application.DTOs.Terms;

/// <summary>Một kỳ học — <c>GET /api/terms</c> (quản trị viên).</summary>
public sealed record AcademicTermDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Ngày bắt đầu, <c>yyyy-MM-dd</c>; vắng khi chưa đặt.</summary>
    public DateOnly? StartsOn { get; init; }

    public DateOnly? EndsOn { get; init; }

    /// <summary>Kỳ hiện tại của nhà trường (tối đa một).</summary>
    public required bool IsCurrent { get; init; }

    /// <summary>Số lớp thuộc kỳ này (kể cả lớp nháp). Kỳ còn lớp thì không xoá được.</summary>
    public required int GroupCount { get; init; }
}
