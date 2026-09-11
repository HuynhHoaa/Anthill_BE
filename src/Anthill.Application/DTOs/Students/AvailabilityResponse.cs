namespace Anthill.Application.DTOs.Students;

/// <summary>
/// Kết quả của <c>GET /api/students/availability</c>.
/// </summary>
/// <remarks>
/// Ba trường đầu nullable và cấu hình JSON bỏ hẳn trường NULL khỏi response, nên client hỏi một
/// ô thì nhận về đúng một ô. Điều đó tránh được hiểu nhầm nguy hiểm nhất ở đây: <c>false</c> là
/// "đã có người dùng", còn "không hỏi" phải là <b>vắng mặt</b> chứ không phải <c>false</c>.
/// </remarks>
public sealed record AvailabilityResponse
{
    /// <summary>True nghĩa là email còn trống, dùng được.</summary>
    public bool? EmailAvailable { get; init; }

    public bool? UsernameAvailable { get; init; }

    public bool? CodeAvailable { get; init; }

    /// <summary>Mọi trường được hỏi đều còn trống — đủ để bật/tắt nút Lưu bằng một phép kiểm.</summary>
    public required bool AllAvailable { get; init; }
}
