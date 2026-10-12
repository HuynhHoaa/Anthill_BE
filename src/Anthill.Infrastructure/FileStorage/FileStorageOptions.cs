using Anthill.Domain.Constants;

namespace Anthill.Infrastructure.FileStorage;

/// <summary>
/// Cấu hình nơi cất ảnh câu hỏi và giới hạn của nó.
/// </summary>
/// <remarks>
/// Có hai nơi cất, chọn bằng <see cref="Provider"/>: đĩa của chính máy chạy API (mặc định, cho máy lập trình)
/// và Cloudinary (cho production, vì Render xoá sạch đĩa mỗi lần khởi động lại — ảnh sẽ biến mất sau lần deploy
/// kế tiếp). Cả hai nằm sau <c>IFileService</c> và cùng cấp một đường dẫn tương đối <c>/uploads/...</c>, nên
/// database, validator và frontend không biết ảnh đang nằm ở đâu.
/// </remarks>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public const string LocalProvider = "Local";
    public const string CloudinaryProvider = "Cloudinary";

    /// <summary><see cref="LocalProvider"/> hoặc <see cref="CloudinaryProvider"/>.</summary>
    public string Provider { get; set; } = LocalProvider;

    /// <summary>Thông tin tài khoản Cloudinary; chỉ đọc khi <see cref="Provider"/> là Cloudinary.</summary>
    public CloudinaryOptions Cloudinary { get; set; } = new();

    public bool UsesCloudinary => string.Equals(Provider, CloudinaryProvider, StringComparison.OrdinalIgnoreCase);

    /// <summary>Dừng ngay lúc khởi động nếu chọn một nơi cất không có, hoặc chọn Cloudinary mà thiếu khoá.</summary>
    public void Validate()
    {
        if (!UsesCloudinary)
        {
            if (!string.Equals(Provider, LocalProvider, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"'{SectionName}:Provider' chỉ nhận '{LocalProvider}' hoặc '{CloudinaryProvider}', đang là '{Provider}'.");
            }

            return;
        }

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(Cloudinary.CloudName)) missing.Add("CloudName");
        if (string.IsNullOrWhiteSpace(Cloudinary.ApiKey)) missing.Add("ApiKey");
        if (string.IsNullOrWhiteSpace(Cloudinary.ApiSecret)) missing.Add("ApiSecret");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Chọn Cloudinary nhưng thiếu {string.Join(", ", missing)}. Đặt bằng biến môi trường "
                + $"{SectionName}__Cloudinary__CloudName, {SectionName}__Cloudinary__ApiKey, {SectionName}__Cloudinary__ApiSecret.");
        }
    }

    /// <summary>Thư mục gốc trên đĩa, tương đối so với thư mục nội dung của ứng dụng.</summary>
    public string RootPath { get; set; } = "wwwroot";

    /// <summary>Thư mục con dành cho file người dùng tải lên, nằm trong <see cref="RootPath"/>.</summary>
    public string UploadFolder { get; set; } = "uploads";

    /// <summary>
    /// Tiền tố URL công khai. Phải khớp đường dẫn mà middleware phục vụ file tĩnh gắn vào, và
    /// khớp <see cref="FileStorageConstants.PublicRequestPathPrefix"/> mà
    /// <c>SaveQuestionRequestValidator</c> dùng để chấp nhận <c>ImageUrl</c> — đổi giá trị này
    /// trong cấu hình mà không đổi cả hằng số kia thì ảnh hợp lệ sẽ bị validator từ chối.
    /// </summary>
    public string RequestPath { get; set; } = FileStorageConstants.PublicRequestPathPrefix;

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
