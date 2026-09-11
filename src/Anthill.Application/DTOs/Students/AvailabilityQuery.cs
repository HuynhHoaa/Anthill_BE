namespace Anthill.Application.DTOs.Students;

/// <summary>
/// Tham số của <c>GET /api/students/availability</c> — kiểm tra ba định danh đã có ai dùng chưa.
/// </summary>
/// <remarks>
/// <para>
/// Endpoint này tồn tại để thay việc frontend tải 500 bản ghi về so sánh tại chỗ (§4.3). Ngoài
/// chuyện tốn băng thông, cách cũ còn sai: nó chỉ so được với 500 bản ghi đầu tiên, nên hệ thống
/// vượt quá số đó là bắt đầu cho tạo trùng — rồi unique index ở database chặn lại bằng một lỗi
/// 500 không ai hiểu.
/// </para>
/// <para>
/// Trường nào bỏ trống thì không kiểm, và trường tương ứng trong response cũng vắng mặt. Nhờ vậy
/// ô nhập nào vừa rời con trỏ thì chỉ hỏi đúng ô đó.
/// </para>
/// </remarks>
public sealed class AvailabilityQuery
{
    public string? Email { get; set; }

    public string? Username { get; set; }

    public string? Code { get; set; }

    /// <summary>
    /// Id học sinh đang được sửa, để form sửa hồ sơ không tự báo trùng với chính mình (§5).
    /// Bỏ trống khi đang tạo mới.
    /// </summary>
    public int? ExcludeStudentId { get; set; }

    /// <summary>Không hỏi gì cả thì không có gì để trả lời — service dùng để trả lỗi sớm.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Email)
        && string.IsNullOrWhiteSpace(Username)
        && string.IsNullOrWhiteSpace(Code);
}
