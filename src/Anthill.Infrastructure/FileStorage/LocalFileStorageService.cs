using Anthill.Application.Common;
using Anthill.Application.DTOs.Files;
using Anthill.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace Anthill.Infrastructure.FileStorage;

/// <summary>
/// Cài đặt <see cref="IFileService"/> bằng đĩa của chính máy chạy API.
/// </summary>
/// <remarks>
/// <para>
/// <b>Upload là bề mặt tấn công lớn nhất của một API.</b> Bốn hàng rào ở đây, xếp theo thứ tự
/// một kẻ tấn công phải vượt qua:
/// </para>
/// <list type="number">
/// <item><b>Dung lượng</b> — chặn trước khi đọc, để một file 2 GB không kịp lấp đầy đĩa.</item>
/// <item><b>Phần mở rộng</b> — chỉ năm đuôi ảnh. Đây là hàng rào yếu nhất vì client tự đặt tên,
/// nhưng nó chặn được trường hợp phổ biến nhất: đuôi <c>.html</c> hay <c>.svg</c> phục vụ từ cùng
/// origin sẽ chạy được script trong trình duyệt của người xem.</item>
/// <item><b>Magic bytes</b> — hàng rào thật. Đọc vài byte đầu để biết nội dung <i>thật sự</i> là
/// gì; đổi đuôi một file <c>.exe</c> thành <c>.png</c> không qua được bước này. Loại ảnh trả về
/// cho client cũng suy từ đây, không phải từ header <c>Content-Type</c> client khai.</item>
/// <item><b>Tên file do server sinh</b> — tên người dùng gửi lên không bao giờ chạm tới đĩa, nên
/// <c>../../appsettings.json</c> hay một ký tự điều khiển trong tên đều vô hại.</item>
/// </list>
/// <para>
/// SVG cố ý <b>không</b> nằm trong danh sách cho phép dù nó là định dạng ảnh: file SVG chứa được
/// thẻ <c>&lt;script&gt;</c>, và một ảnh phục vụ từ cùng origin với API là một lỗ XSS hoàn chỉnh.
/// </para>
/// </remarks>
public sealed class LocalFileStorageService : IFileService
{
    private readonly FileStorageOptions _options;

    public LocalFileStorageService(IOptions<FileStorageOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<Result<UploadedFileDto>> UploadImageAsync(
        FileUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Ba hàng rào đầu (dung lượng, đuôi, magic bytes) dùng chung với mọi nơi cất ảnh khác.
        var validated = await ImageUploadRules.ValidateAsync(request, _options, cancellationToken);
        if (validated.IsFailure)
        {
            return Result.Failure<UploadedFileDto>(validated.Error!, validated.ErrorCode);
        }

        var image = validated.ValueOrThrow;
        var extension = image.Extension;
        var contentType = image.ContentType;
        var signature = image.Signature;

        // Chia theo tháng để một thư mục không phình tới hàng chục nghìn file — mức mà nhiều hệ
        // thống tệp bắt đầu chậm rõ rệt khi liệt kê.
        var monthFolder = DateTime.UtcNow.ToString("yyyyMM");
        var fileName = $"{Guid.NewGuid():N}{extension}";

        var directory = Path.Combine(UploadRoot, monthFolder);
        Directory.CreateDirectory(directory);

        var fullPath = Path.Combine(directory, fileName);
        long written;

        try
        {
            // FileMode.CreateNew chứ không phải Create: tên là GUID nên đụng độ gần như không thể,
            // và nếu điều gần-như-không-thể xảy ra thì báo lỗi vẫn hơn là ghi đè ảnh của người khác.
            await using var target = new FileStream(
                fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

            // Ghi lại phần đầu đã đọc để nhận dạng, rồi mới nối phần còn lại của luồng.
            await target.WriteAsync(signature, cancellationToken);
            await request.Content.CopyToAsync(target, cancellationToken);

            written = target.Length;
        }
        catch (IOException exception)
        {
            return Result.Failure<UploadedFileDto>(
                $"Không lưu được ảnh: {exception.Message}", ErrorCode.Internal);
        }

        // Luôn dùng dấu gạch chéo xuôi: đây là URL, không phải đường dẫn đĩa. Path.Combine trên
        // Windows sinh dấu gạch ngược và trình duyệt không hiểu.
        var url = $"{_options.RequestPath.TrimEnd('/')}/{monthFolder}/{fileName}";

        return Result.Success(new UploadedFileDto
        {
            Url = url,
            FileName = fileName,
            SizeBytes = written,
            ContentType = contentType
        });
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(string? relativeUrl, CancellationToken cancellationToken = default)
    {
        if (ResolveSafePath(relativeUrl) is not { } fullPath)
        {
            // Đường dẫn nằm ngoài thư mục upload: từ chối, và cố ý không nói vì sao. Người gọi hợp
            // lệ chỉ truyền lại đúng URL server đã cấp nên không bao giờ gặp nhánh này.
            return Task.FromResult(Result.Failure("Đường dẫn ảnh không hợp lệ."));
        }

        try
        {
            // File.Delete không kêu ca khi file đã không còn — đúng ý "bảo đảm nó không tồn tại".
            File.Delete(fullPath);
            return Task.FromResult(Result.Success());
        }
        catch (IOException exception)
        {
            return Task.FromResult(Result.Failure(
                $"Không xoá được ảnh: {exception.Message}", ErrorCode.Internal));
        }
    }

    private string UploadRoot => Path.Combine(_options.RootPath, _options.UploadFolder);

    /// <summary>
    /// Đổi URL tương đối thành đường dẫn đĩa, và <b>chỉ khi</b> nó thật sự nằm trong thư mục upload.
    /// </summary>
    /// <remarks>
    /// Phép so sánh làm trên đường dẫn đã <c>GetFullPath</c> — tức đã rút gọn hết <c>..</c> và
    /// <c>.</c>. Kiểm trên chuỗi thô thì <c>/uploads/../../appsettings.json</c> vẫn "bắt đầu bằng
    /// /uploads" và lọt qua.
    /// </remarks>
    private string? ResolveSafePath(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return null;
        }

        var prefix = _options.RequestPath.TrimEnd('/');
        var trimmed = relativeUrl.Trim();

        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relative = trimmed[prefix.Length..].TrimStart('/');
        if (relative.Length == 0)
        {
            return null;
        }

        var root = Path.GetFullPath(UploadRoot);
        var candidate = Path.GetFullPath(Path.Combine(root, relative));

        // Ràng buộc dấu phân cách ở cuối root: nếu không, thư mục "uploads_backup" cũng thoả điều
        // kiện "bắt đầu bằng .../uploads".
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }
}
