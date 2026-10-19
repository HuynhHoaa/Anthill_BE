namespace Anthill.Application.DTOs.Students;

/// <summary>
/// Tóm tắt một lớp mà học sinh đang học: đủ để vẽ nhãn lớp và biết lớp đó còn nhận bài không.
/// </summary>
public sealed record StudentGroupRefDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Lớp đã khoá: chỉ đọc, không giao thêm bài, không xếp thêm em.</summary>
    public required bool IsLocked { get; init; }
}
