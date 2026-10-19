namespace Anthill.Application.DTOs.Terms;

/// <summary>Tạo hoặc sửa một kỳ học.</summary>
public sealed record SaveAcademicTermRequest
{
    public required string Name { get; init; }

    public DateOnly? StartsOn { get; init; }

    public DateOnly? EndsOn { get; init; }

    /// <summary>Đặt làm kỳ hiện tại: kỳ đang là hiện tại (nếu có) tự thôi.</summary>
    public bool IsCurrent { get; init; }
}
