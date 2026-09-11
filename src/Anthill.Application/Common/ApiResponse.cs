namespace Anthill.Application.Common;

/// <summary>
/// Vỏ lỗi thống nhất cho toàn hệ thống, đúng hình dạng <c>{ message, errors? }</c> mà
/// <c>apiClient.readErrorMessage</c> của frontend đang đọc (§1 hợp đồng).
/// </summary>
/// <remarks>
/// Cố ý <b>không</b> có vỏ <c>{ success, data }</c> cho trường hợp thành công:
/// <c>apiRequest&lt;T&gt;</c> của frontend trả thẳng body đã parse thành <c>T</c>, không bóc lớp nào.
/// Bọc thêm một lớp ở đây đồng nghĩa mọi trang FE phải sửa — trái mục tiêu "chữ ký hàm giữ nguyên".
/// Vì vậy: thành công trả thẳng payload, thất bại trả <see cref="ApiErrorResponse"/>.
/// </remarks>
public sealed record ApiErrorResponse
{
    public required string Message { get; init; }

    /// <summary>Lỗi theo từng trường cho form. Bỏ hẳn khỏi JSON khi không có.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static ApiErrorResponse From(Result result) => new()
    {
        Message = result.Error ?? "Yêu cầu không thành công.",
        Errors = result.Errors
    };

    public static ApiErrorResponse Of(string message) => new() { Message = message };
}

/// <summary>
/// Body cho endpoint thành công nhưng không có dữ liệu để trả (đăng xuất, đánh dấu đã đọc...).
/// Dùng thay cho 204 khi muốn frontend có sẵn một câu để hiện lên toast.
/// </summary>
public sealed record ApiMessageResponse
{
    public required string Message { get; init; }

    public static ApiMessageResponse Of(string message) => new() { Message = message };
}
