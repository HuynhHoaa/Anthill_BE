namespace EduQuiz.Application.DTOs.Files;

/// <summary>
/// Kết quả của <c>POST /api/uploads/images</c>.
/// </summary>
/// <remarks>
/// <see cref="Url"/> là <b>đường dẫn tương đối</b> (<c>/uploads/202609/a1b2c3.png</c>), không phải
/// URL tuyệt đối. Lưu tuyệt đối xuống database nghĩa là đổi tên miền hay chuyển từ máy lập trình
/// lên production là mọi ảnh cũ trỏ vào hư không. Frontend ghép với <c>VITE_API_BASE_URL</c> mà
/// nó vốn đã có.
/// </remarks>
public sealed record UploadedFileDto
{
    /// <summary>Đường dẫn tương đối để lưu vào <c>Question.ImageUrl</c> hoặc <c>AnswerOption.ImageUrl</c>.</summary>
    public required string Url { get; init; }

    /// <summary>Tên file server đã đặt — ngẫu nhiên, không phải tên người dùng gửi lên.</summary>
    public required string FileName { get; init; }

    public required long SizeBytes { get; init; }

    /// <summary>Loại ảnh <b>thật</b>, suy từ magic bytes chứ không phải từ header client khai.</summary>
    public required string ContentType { get; init; }
}
