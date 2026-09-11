namespace Anthill.Application.DTOs.Students;

/// <summary>Thân request của <c>POST /api/students/{id}/request-deletion</c>.</summary>
public sealed record RequestDeletionRequest
{
    /// <summary>
    /// Vì sao xoá tài khoản này. <b>Bắt buộc</b>, tối thiểu 10 ký tự.
    /// </summary>
    /// <remarks>
    /// Bắt buộc chứ không phải tuỳ chọn, vì một ô ghi chú không bắt buộc thì ai cũng bỏ trống và
    /// tính năng thành vô dụng đúng vào lúc cần tới nó. Mười ký tự đủ để chặn "xoá" hay "ok" mà
    /// vẫn không cản người có việc thật.
    ///
    /// Lý do sống lâu hơn chính tài khoản: sau khi dữ liệu cá nhân bị xoá sạch, đây là chỗ duy
    /// nhất còn giải thích được vì sao lớp hụt một em.
    /// </remarks>
    public required string Reason { get; init; }
}
