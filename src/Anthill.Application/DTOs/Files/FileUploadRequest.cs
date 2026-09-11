namespace Anthill.Application.DTOs.Files;

/// <summary>
/// Một file đang được tải lên, mô tả bằng những thứ tầng Application hiểu được.
/// </summary>
/// <remarks>
/// <para>
/// Cố ý <b>không</b> nhận <c>IFormFile</c>: kiểu đó thuộc ASP.NET Core, mà tầng Application không
/// được biết gì về web framework. Controller bóc <c>IFormFile</c> thành bốn trường dưới đây.
/// </para>
/// <para>
/// <see cref="FileName"/> là tên người dùng đặt, nên nó là <b>dữ liệu không tin được</b>: có thể
/// chứa <c>../</c>, ký tự điều khiển, hoặc một phần mở rộng nói dối về nội dung thật. Nó chỉ được
/// dùng để đọc phần mở rộng và để đối chiếu; tên file lưu xuống đĩa luôn do server sinh ngẫu nhiên.
/// </para>
/// </remarks>
public sealed record FileUploadRequest
{
    /// <summary>Luồng nội dung. Bên gọi chịu trách nhiệm đóng; service chỉ đọc.</summary>
    public required Stream Content { get; init; }

    /// <summary>Tên file gốc do trình duyệt gửi lên — <b>không tin được</b>, xem phần ghi chú.</summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Content-Type do trình duyệt khai. Cũng không tin được: nó do client đặt, và đổi được bằng
    /// một dòng lệnh. Service vẫn đọc magic bytes để biết nội dung thật.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>Dung lượng tính bằng byte, để chặn sớm trước khi đọc hết luồng.</summary>
    public required long Length { get; init; }
}
