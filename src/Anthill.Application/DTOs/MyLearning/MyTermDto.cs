namespace Anthill.Application.DTOs.MyLearning;

/// <summary>
/// Một kỳ học em có lớp — <c>GET /api/my/terms</c>. Chỉ gồm kỳ mà em đang có ít nhất một lớp đã công bố, theo thứ tự: kỳ hiện tại trước,
/// rồi kỳ mới bắt đầu trước. Lớp chưa phân kỳ gộp thành một mục cuối danh sách với <c>Id = 0</c> ("Chưa phân kỳ").
/// </summary>
public sealed record MyTermDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public DateOnly? StartsOn { get; init; }

    public DateOnly? EndsOn { get; init; }

    /// <summary>Kỳ hiện tại của nhà trường; giao diện mở sẵn kỳ này (nếu em có lớp trong đó).</summary>
    public required bool IsCurrent { get; init; }

    /// <summary>Số lớp của em trong kỳ.</summary>
    public required int GroupCount { get; init; }
}
