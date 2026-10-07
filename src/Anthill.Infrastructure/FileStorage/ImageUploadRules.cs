using Anthill.Application.Common;
using Anthill.Application.DTOs.Files;

namespace Anthill.Infrastructure.FileStorage;

/// <summary>Một ảnh đã qua đủ các hàng rào kiểm tra, cùng phần đầu đã đọc để nhận dạng.</summary>
/// <param name="Extension">Phần mở rộng viết thường, có dấu chấm (<c>.png</c>).</param>
/// <param name="ContentType">Loại ảnh THẬT, suy từ magic bytes.</param>
/// <param name="Signature">Những byte đầu đã đọc khỏi luồng; người ghi phải ghi lại chúng trước phần còn lại.</param>
internal sealed record ValidatedImage(string Extension, string ContentType, byte[] Signature);

/// <summary>
/// Ba hàng rào đầu của việc tải ảnh lên — dung lượng, phần mở rộng, magic bytes — dùng chung cho mọi nơi cất
/// ảnh (đĩa, Cloudinary). Hàng rào thứ tư, tên file do server sinh, thuộc về từng nơi cất vì nó phụ thuộc cách
/// nơi đó đặt tên.
/// </summary>
/// <remarks>
/// Tách ra khỏi <see cref="LocalFileStorageService"/> để đổi nơi cất không kéo theo việc viết lại, hay tệ hơn là
/// quên, các hàng rào bảo mật: một nơi cất mới mà thiếu magic bytes là một lỗ hổng, không phải một tính năng.
/// Giải thích vì sao từng hàng rào có mặt nằm ở tài liệu của <see cref="LocalFileStorageService"/>.
/// </remarks>
internal static class ImageUploadRules
{
    /// <summary>Đuôi file được phép, ghép với loại ảnh tương ứng. SVG cố ý không có mặt.</summary>
    public static readonly IReadOnlyDictionary<string, string> AllowedExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp"
        };

    /// <summary>
    /// Số byte cần đọc để nhận dạng mọi định dạng trong danh sách. WebP cần nhiều nhất: bốn byte
    /// <c>RIFF</c>, bốn byte độ dài, rồi bốn byte <c>WEBP</c>.
    /// </summary>
    private const int SignatureLength = 12;

    public static async Task<Result<ValidatedImage>> ValidateAsync(
        FileUploadRequest request,
        FileStorageOptions options,
        CancellationToken cancellationToken)
    {
        if (request.Length <= 0)
        {
            return Result.Failure<ValidatedImage>("File rỗng.");
        }

        // Chặn theo Length trước khi đọc một byte nào: đọc rồi mới đo nghĩa là nơi cất đã phải chứa nó.
        if (request.Length > options.MaxBytes)
        {
            return Result.Failure<ValidatedImage>(options.TooLargeMessage);
        }

        var extension = Path.GetExtension(request.FileName)?.Trim() ?? string.Empty;
        if (!AllowedExtensions.ContainsKey(extension))
        {
            return Result.Failure<ValidatedImage>(
                $"Chỉ chấp nhận ảnh {string.Join(", ", AllowedExtensions.Keys)}.");
        }

        var signature = await ReadSignatureAsync(request.Content, cancellationToken);
        if (DetectContentType(signature) is not { } contentType)
        {
            return Result.Failure<ValidatedImage>("Nội dung file không phải là ảnh hợp lệ.");
        }

        // Đuôi nói một đằng, nội dung một nẻo — không phải lúc nào cũng là tấn công (người dùng đổi
        // tên nhầm), nhưng lưu lại thì tên file sẽ nói dối về nội dung của chính nó.
        if (!string.Equals(AllowedExtensions[extension], contentType, StringComparison.Ordinal))
        {
            return Result.Failure<ValidatedImage>(
                "Phần mở rộng của file không khớp với nội dung thật bên trong.");
        }

        return Result.Success(new ValidatedImage(extension.ToLowerInvariant(), contentType, signature));
    }

    private static async Task<byte[]> ReadSignatureAsync(Stream content, CancellationToken cancellationToken)
    {
        var buffer = new byte[SignatureLength];
        var read = await content.ReadAtLeastAsync(
            buffer, SignatureLength, throwOnEndOfStream: false, cancellationToken);

        return read == SignatureLength ? buffer : buffer[..read];
    }

    /// <summary>
    /// Nhận dạng định dạng ảnh từ vài byte đầu — thứ duy nhất ở đây mà client không giả được bằng
    /// cách đổi tên file hay sửa header.
    /// </summary>
    private static string? DetectContentType(ReadOnlySpan<byte> signature)
    {
        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (StartsWith(signature, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        // JPEG: FF D8 FF
        if (StartsWith(signature, [0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        // GIF: "GIF87a" hoặc "GIF89a" — sáu byte đầu đủ để phân biệt cả hai phiên bản.
        if (StartsWith(signature, "GIF87a"u8) || StartsWith(signature, "GIF89a"u8))
        {
            return "image/gif";
        }

        // WebP: "RIFF" ở byte 0-3, "WEBP" ở byte 8-11. Bốn byte ở giữa là độ dài file nên bỏ qua.
        if (StartsWith(signature, "RIFF"u8)
            && signature.Length >= 12
            && signature[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix) =>
        value.Length >= prefix.Length && value[..prefix.Length].SequenceEqual(prefix);
}
