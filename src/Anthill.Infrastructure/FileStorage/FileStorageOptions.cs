namespace Anthill.Infrastructure.FileStorage;

/// <summary>
/// Cấu hình nơi cất ảnh câu hỏi và giới hạn của nó.
/// </summary>
/// <remarks>
/// Bản cài đặt hiện tại ghi xuống đĩa của chính máy chạy API. Kế hoạch deploy chọn Cloudinary cho
/// production, vì Render xoá sạch đĩa mỗi lần khởi động lại — ảnh sẽ biến mất sau lần deploy kế
/// tiếp. Toàn bộ phần đó nằm sau <c>IFileService</c>, nên đổi sang Cloudinary là thêm một lớp cài
/// đặt mới và đổi một dòng đăng ký; không controller nào phải sửa.
/// </remarks>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Thư mục gốc trên đĩa, tương đối so với thư mục nội dung của ứng dụng.</summary>
    public string RootPath { get; set; } = "wwwroot";

    /// <summary>Thư mục con dành cho file người dùng tải lên, nằm trong <see cref="RootPath"/>.</summary>
    public string UploadFolder { get; set; } = "uploads";

    /// <summary>Tiền tố URL công khai. Phải khớp đường dẫn mà middleware phục vụ file tĩnh gắn vào.</summary>
    public string RequestPath { get; set; } = "/uploads";

    /// <summary>
    /// Trần dung lượng mỗi ảnh. 4 MB đủ cho một ảnh chụp đề bài, và đủ nhỏ để một học sinh dùng
    /// 3G vẫn tải được câu hỏi về trong lúc làm bài.
    /// </summary>
    public long MaxBytes { get; set; } = 4 * 1024 * 1024;

    /// <summary>
    /// Phần dư cho vỏ multipart bọc quanh ảnh (boundary, header của từng phần, tên file). Thực tế
    /// chỉ vài trăm byte; lấy 64 KB cho rộng rãi, vì ảnh lọt vào khoảng dư này vẫn bị
    /// <see cref="MaxBytes"/> chặn ở service với cùng một thông điệp.
    /// </summary>
    public const long MultipartOverheadBytes = 64 * 1024;

    /// <summary>
    /// Trần cả thân request upload: một ảnh đúng <see cref="MaxBytes"/> cộng vỏ multipart. Server
    /// chặn ở mức này trước khi đọc form, thay vì đọc hết rồi mới đo.
    /// </summary>
    public long MaxRequestBytes => MaxBytes + MultipartOverheadBytes;

    /// <summary>
    /// Một câu cho mọi đường từ chối ảnh quá lớn — chặn ở đầu vào hay ở service — để người dùng không
    /// thấy hai thông điệp khác nhau cho cùng một lỗi.
    /// </summary>
    public string TooLargeMessage => $"Ảnh vượt quá dung lượng cho phép ({MaxBytes / (1024 * 1024)} MB).";
}
