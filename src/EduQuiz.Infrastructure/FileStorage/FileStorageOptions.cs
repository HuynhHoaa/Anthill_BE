namespace EduQuiz.Infrastructure.FileStorage;

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
}
