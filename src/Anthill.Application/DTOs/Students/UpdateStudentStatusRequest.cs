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

    /// <summary>
    /// Lý do khoá. <b>Bắt buộc</b> khi <see cref="Status"/> là <c>"locked"</c> (10–512 ký tự sau khi cắt khoảng
    /// trắng); mở khoá thì không cần và nếu có gửi cũng không được lưu.
    /// </summary>
    public string? Reason { get; init; }
}
