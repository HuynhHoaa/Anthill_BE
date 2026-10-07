using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Anthill.Application.Common;
using Anthill.Application.DTOs.Files;
using Anthill.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace Anthill.Infrastructure.FileStorage;

/// <summary>
/// Cài đặt <see cref="IFileService"/> bằng Cloudinary: ảnh nằm ngoài máy chạy API nên sống sót qua mỗi lần khởi
/// động lại, điều mà đĩa của Render không làm được.
/// </summary>
/// <remarks>
/// <para>
/// <b>Database vẫn chỉ biết đường dẫn tương đối.</b> Ảnh được cấp URL <c>/uploads/202610/a1b2....png</c> giống hệt
/// bản ghi xuống đĩa; API chuyển hướng đường dẫn đó sang địa chỉ giao hàng của Cloudinary (xem
/// <see cref="TryBuildDeliveryUrl"/>). Nhờ vậy validator, dữ liệu cũ và frontend không đổi gì, và ảnh vẫn không
/// dính vào tên miền hay tài khoản Cloudinary cụ thể nào.
/// </para>
/// <para>
/// Ba hàng rào kiểm tra file là của <see cref="ImageUploadRules"/>, dùng chung với bản ghi xuống đĩa. Tên file
/// vẫn do server sinh (GUID), và <c>public_id</c> gửi lên Cloudinary dựng từ đúng GUID đó, không từ tên người
/// dùng. Gọi Cloudinary bằng HTTP trực tiếp (có chữ ký SHA-1 theo tài liệu của họ) thay vì kéo thêm một gói
/// SDK chỉ để có hai lời gọi.
/// </para>
/// </remarks>
public sealed partial class CloudinaryFileService : IFileService
{
    private const string ApiBase = "https://api.cloudinary.com/v1_1";
    private const string DeliveryBase = "https://res.cloudinary.com";

    private readonly HttpClient _http;
    private readonly FileStorageOptions _options;

    public CloudinaryFileService(HttpClient http, IOptions<FileStorageOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    /// <summary>Phần còn lại của đường dẫn sau tiền tố: <c>202610/&lt;32 ký tự hex&gt;.png</c>.</summary>
    [GeneratedRegex(@"^(?<month>\d{6})/(?<guid>[0-9a-f]{32})(?<ext>\.(?:png|jpg|jpeg|gif|webp))$", RegexOptions.CultureInvariant)]
    private static partial Regex StoredPathPattern();

    /// <inheritdoc />
    public async Task<Result<UploadedFileDto>> UploadImageAsync(
        FileUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validated = await ImageUploadRules.ValidateAsync(request, _options, cancellationToken);
        if (validated.IsFailure)
        {
            return Result.Failure<UploadedFileDto>(validated.Error!, validated.ErrorCode);
        }

        var image = validated.ValueOrThrow;

        // Ảnh tối đa vài MB, nên đọc hẳn vào bộ nhớ để gửi đi một lần. Đo lại sau khi đọc: Length do client khai,
        // còn luồng thật có thể dài hơn.
        using var buffer = new MemoryStream();
        await buffer.WriteAsync(image.Signature, cancellationToken);
        await request.Content.CopyToAsync(buffer, cancellationToken);

        if (buffer.Length > _options.MaxBytes)
        {
            return Result.Failure<UploadedFileDto>(_options.TooLargeMessage);
        }

        var month = DateTime.UtcNow.ToString("yyyyMM");
        var guid = Guid.NewGuid().ToString("N");
        var fileName = $"{guid}{image.Extension}";
        var publicId = $"{_options.Cloudinary.Folder.Trim('/')}/{month}/{guid}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        using var form = new MultipartFormDataContent
        {
            { new StringContent(_options.Cloudinary.ApiKey), "api_key" },
            { new StringContent(timestamp), "timestamp" },
            { new StringContent(publicId), "public_id" },
            {
                new StringContent(Sign(
                    [new("public_id", publicId), new("timestamp", timestamp)],
                    _options.Cloudinary.ApiSecret)),
                "signature"
            }
        };

        var file = new ByteArrayContent(buffer.ToArray());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(image.ContentType);
        form.Add(file, "file", fileName);

        try
        {
            using var response = await _http.PostAsync(
                $"{ApiBase}/{_options.Cloudinary.CloudName}/image/upload", form, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<UploadedFileDto>(
                    "Không lưu được ảnh lúc này, thử lại sau.", ErrorCode.Internal);
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            var size = json.RootElement.TryGetProperty("bytes", out var bytes) && bytes.TryGetInt64(out var value)
                ? value
                : buffer.Length;

            return Result.Success(new UploadedFileDto
            {
                // Đường dẫn tương đối, y hệt bản ghi xuống đĩa: API sẽ chuyển hướng nó sang Cloudinary.
                Url = $"{_options.RequestPath.TrimEnd('/')}/{month}/{fileName}",
                FileName = fileName,
                SizeBytes = size,
                ContentType = image.ContentType
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Result.Failure<UploadedFileDto>(
                "Không lưu được ảnh lúc này, thử lại sau.", ErrorCode.Internal);
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(string? relativeUrl, CancellationToken cancellationToken = default)
    {
        if (TryParseStoredPath(_options, relativeUrl) is not { } stored)
        {
            // Không phải đường dẫn do chính hệ thống cấp: từ chối, và cố ý không nói vì sao.
            return Result.Failure("Đường dẫn ảnh không hợp lệ.");
        }

        var publicId = $"{_options.Cloudinary.Folder.Trim('/')}/{stored.Month}/{stored.Guid}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        using var form = new FormUrlEncodedContent(
        [
            new("api_key", _options.Cloudinary.ApiKey),
            new("timestamp", timestamp),
            new("public_id", publicId),
            new("signature", Sign(
                [new("public_id", publicId), new("timestamp", timestamp)],
                _options.Cloudinary.ApiSecret))
        ]);

        try
        {
            using var response = await _http.PostAsync(
                $"{ApiBase}/{_options.Cloudinary.CloudName}/image/destroy", form, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure("Không xoá được ảnh lúc này.", ErrorCode.Internal);
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            var outcome = json.RootElement.TryGetProperty("result", out var result) ? result.GetString() : null;

            // "not found" cũng là thành công: mục đích là bảo đảm ảnh không còn, mà điều đó đã đúng sẵn.
            return outcome is "ok" or "not found"
                ? Result.Success()
                : Result.Failure("Không xoá được ảnh lúc này.", ErrorCode.Internal);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Result.Failure("Không xoá được ảnh lúc này.", ErrorCode.Internal);
        }
    }

    /// <summary>
    /// Đổi đường dẫn tương đối của hệ thống thành địa chỉ giao hàng của Cloudinary. Trả <c>false</c> với mọi thứ
    /// không đúng khuôn <c>/uploads/yyyyMM/&lt;guid&gt;.&lt;đuôi ảnh&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Địa chỉ đích dựng từ từng phần đã khớp khuôn chứ không ghép từ chuỗi người dùng gửi lên, nên endpoint chuyển
    /// hướng không thể bị dùng làm "open redirect" tới một nơi khác.
    /// </remarks>
    public static bool TryBuildDeliveryUrl(FileStorageOptions options, string? relativeUrl, out string deliveryUrl)
    {
        ArgumentNullException.ThrowIfNull(options);

        deliveryUrl = string.Empty;
        if (TryParseStoredPath(options, relativeUrl) is not { } stored)
        {
            return false;
        }

        deliveryUrl = $"{DeliveryBase}/{options.Cloudinary.CloudName}/image/upload/"
                      + $"{options.Cloudinary.Folder.Trim('/')}/{stored.Month}/{stored.Guid}{stored.Extension}";
        return true;
    }

    private readonly record struct StoredPath(string Month, string Guid, string Extension);

    private static StoredPath? TryParseStoredPath(FileStorageOptions options, string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return null;
        }

        var prefix = options.RequestPath.TrimEnd('/') + "/";
        var trimmed = relativeUrl.Trim();
        if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var match = StoredPathPattern().Match(trimmed[prefix.Length..]);
        return match.Success
            ? new StoredPath(match.Groups["month"].Value, match.Groups["guid"].Value, match.Groups["ext"].Value)
            : null;
    }

    /// <summary>
    /// Chữ ký của một lời gọi Cloudinary: các tham số (trừ file, api_key, cloud_name, resource_type) xếp theo tên,
    /// nối thành <c>tên=giá trị</c> bằng dấu <c>&amp;</c>, thêm bí mật vào cuối rồi băm SHA-1.
    /// </summary>
    public static string Sign(IEnumerable<KeyValuePair<string, string>> parameters, string apiSecret)
    {
        var toSign = string.Join(
            "&",
            parameters
                .Where(parameter => !string.IsNullOrEmpty(parameter.Value))
                .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .Select(parameter => $"{parameter.Key}={parameter.Value}"));

        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(toSign + apiSecret))).ToLowerInvariant();
    }
}
