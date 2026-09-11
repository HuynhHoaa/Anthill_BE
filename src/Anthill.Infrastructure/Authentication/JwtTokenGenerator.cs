using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anthill.Application.Interfaces.Security;
using Anthill.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Anthill.Infrastructure.Authentication;

/// <summary>
/// Cấp access token HS256 với đúng bộ claim §4.1 yêu cầu:
/// <c>sub</c>, <c>role</c>, <c>name</c>, <c>subject_id</c> (giáo viên) và <c>security_stamp</c>.
/// </summary>
/// <remarks>
/// <para>
/// Phần ký tự viết tay bằng <see cref="HMACSHA256"/> thay vì gọi thư viện: gói
/// <c>Microsoft.AspNetCore.Authentication.JwtBearer</c> nằm ở tầng Api, còn tầng Infrastructure
/// không được thêm dependency mới. Đây là thoả hiệp an toàn vì phần khó của JWT là
/// <b>kiểm</b> token, và việc đó vẫn do middleware JwtBearer đã được kiểm chứng đảm nhiệm —
/// ở đây chỉ nối ba đoạn base64url rồi ký, đúng như RFC 7519 mô tả.
/// </para>
/// <para>
/// Claim <c>role</c> giữ nguyên tên enum .NET (<c>Admin</c>/<c>Teacher</c>/<c>Student</c>) để khớp
/// <c>SystemRoles</c> và <c>[Authorize(Roles = ...)]</c>. Trường <c>role</c> trong hồ sơ trả về cho
/// frontend lại là chữ thường — hai chỗ cố ý khác nhau, xem <c>UserMappingExtensions</c>.
/// </para>
/// </remarks>
public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>Phần header của HS256 không đổi theo token, tính sẵn một lần.</summary>
    private static readonly string EncodedHeader = Base64UrlEncode(
        JsonSerializer.SerializeToUtf8Bytes(
            new Dictionary<string, object> { ["alg"] = "HS256", ["typ"] = "JWT" },
            JsonOptions));

    private readonly JwtOptions _options;
    private readonly byte[] _signingKey;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _options.Validate();
        _signingKey = Encoding.UTF8.GetBytes(_options.SecretKey);
    }

    /// <summary>
    /// Mọi claim đọc từ entity lấy trong database, không có giá trị nào đến từ request —
    /// §3 quy tắc ⑤ (gửi <c>role: 'admin'</c> lúc đăng nhập không được biến ai thành quản trị viên).
    /// </summary>
    public AccessTokenResult GenerateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.ExpiryMinutes);

        var claims = new Dictionary<string, object>
        {
            ["sub"] = user.Id.ToString(CultureInfo.InvariantCulture),
            ["role"] = user.Role.ToString(),
            ["name"] = user.FullName,

            // Lệch với giá trị trong database nghĩa là tài khoản đã bị khoá hoặc đổi mật khẩu
            // sau khi token được cấp — token còn hạn vẫn phải bị từ chối (§5).
            ["security_stamp"] = user.SecurityStamp,

            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iss"] = _options.Issuer,
            ["aud"] = _options.Audience,
            ["iat"] = ToUnixSeconds(issuedAt),
            ["nbf"] = ToUnixSeconds(issuedAt),
            ["exp"] = ToUnixSeconds(expiresAt)
        };

        // Chỉ giáo viên mới có môn; claim này quyết định thấy được ngân hàng câu hỏi nào (§6).
        if (user.SubjectId.HasValue)
        {
            claims["subject_id"] = user.SubjectId.Value.ToString(CultureInfo.InvariantCulture);
        }

        var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims, JsonOptions));
        var signingInput = $"{EncodedHeader}.{encodedPayload}";
        var signature = Base64UrlEncode(HMACSHA256.HashData(_signingKey, Encoding.UTF8.GetBytes(signingInput)));

        return new AccessTokenResult
        {
            AccessToken = $"{signingInput}.{signature}",
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// 64 byte ngẫu nhiên mật mã học, không mang thông tin — giá trị đối chiếu nằm ở phía server.
    /// </summary>
    public string GenerateRefreshToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(64));

    /// <summary>
    /// Base64 dạng URL-safe theo RFC 4648 §5: bỏ ký tự đệm, đổi <c>+/</c> thành <c>-_</c>.
    /// </summary>
    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static long ToUnixSeconds(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
