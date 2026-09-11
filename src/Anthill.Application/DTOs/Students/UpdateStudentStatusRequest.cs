namespace Anthill.Application.DTOs.Students;

/// <summary>
/// Thân request của <c>PATCH /api/students/{id}/status</c> — khoá hoặc mở khoá tài khoản.
/// </summary>
/// <remarks>
/// Tách khỏi <see cref="UpdateStudentRequest"/> vì đây là thao tác một nút bấm trên bảng danh
/// sách: gộp vào PUT thì mỗi lần khoá một học sinh, frontend phải gửi kèm cả hồ sơ, và một
/// trường nào đó cũ trong form sẽ âm thầm ghi đè dữ liệu mới hơn.
/// </remarks>
public sealed record UpdateStudentStatusRequest
{
    /// <summary><c>"active"</c> hoặc <c>"locked"</c>.</summary>
    public required string Status { get; init; }
}
