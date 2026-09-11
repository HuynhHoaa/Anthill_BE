namespace Anthill.Api.Configuration;

/// <summary>
/// Danh sách origin được phép gọi API, đọc từ mục <c>Cors:AllowedOrigins</c>.
/// </summary>
/// <remarks>
/// <para>
/// Đây là chỗ dễ sai nhất khi ghép frontend với backend, nên quy tắc viết hẳn ra:
/// <b>không bao giờ dùng <c>AllowAnyOrigin</c> chung với <c>AllowCredentials</c></b>. Trình duyệt
/// từ chối cặp đó ngay từ preflight, mà SignalR thì cần <c>AllowCredentials</c> — nên origin
/// buộc phải liệt kê cụ thể.
/// </para>
/// <para>
/// Vì địa chỉ preview của Vercel đổi theo từng lần deploy, cấu hình chấp nhận mẫu có dấu sao
/// (<c>https://*.vercel.app</c>). Mẫu đó không đi thẳng vào <c>WithOrigins</c> được — nó phải qua
/// <c>SetIsOriginAllowed</c>, và middleware CORS sẽ phản hồi lại đúng origin cụ thể của request
/// thay vì dấu sao, nên vẫn dùng được với <c>AllowCredentials</c>.
/// </para>
/// </remarks>
public sealed class CorsPolicyOptions
{
    public const string SectionName = "Cors";

    /// <summary>Tên policy duy nhất của hệ thống; <c>Program.cs</c> gọi <c>UseCors</c> với tên này.</summary>
    public const string PolicyName = "AnthillFrontend";

    private CorsPolicyOptions(IReadOnlyList<string> exactOrigins, IReadOnlyList<string> wildcardPatterns)
    {
        ExactOrigins = exactOrigins;
        WildcardPatterns = wildcardPatterns;
    }

    public IReadOnlyList<string> ExactOrigins { get; }

    public IReadOnlyList<string> WildcardPatterns { get; }

    public bool IsEmpty => ExactOrigins.Count == 0 && WildcardPatterns.Count == 0;

    /// <summary>Mọi origin đã cấu hình, dùng để in ra log lúc khởi động.</summary>
    public IEnumerable<string> All => ExactOrigins.Concat(WildcardPatterns);

    public static CorsPolicyOptions Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Đọc tay từng phần tử thay vì Bind: mục cấu hình này là mảng chuỗi phẳng, và làm vậy thì
        // biến môi trường Cors__AllowedOrigins__0 ghi đè được y hệt như trong appsettings.
        var configured = configuration
            .GetSection($"{SectionName}:AllowedOrigins")
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))

            // Header Origin của trình duyệt không bao giờ có dấu gạch chéo cuối; để nguyên
            // "http://localhost:5173/" trong cấu hình là không khớp gì cả.
            .Select(value => value!.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CorsPolicyOptions(
            configured.Where(origin => !origin.Contains('*', StringComparison.Ordinal)).ToArray(),
            configured.Where(origin => origin.Contains('*', StringComparison.Ordinal)).ToArray());
    }

    /// <summary>Origin của request có nằm trong danh sách cho phép không.</summary>
    public bool IsAllowed(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        var candidate = origin.TrimEnd('/');

        foreach (var allowed in ExactOrigins)
        {
            if (string.Equals(allowed, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var pattern in WildcardPatterns)
        {
            if (MatchesWildcard(pattern, candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// So khớp mẫu một dấu sao: phần thay cho dấu sao không được chứa <c>/</c> hay <c>@</c>, nếu không
    /// <c>https://ke-gian.com/@x.vercel.app</c> cũng lọt qua mẫu <c>https://*.vercel.app</c>.
    /// </summary>
    private static bool MatchesWildcard(string pattern, string origin)
    {
        var starIndex = pattern.IndexOf('*', StringComparison.Ordinal);
        if (starIndex < 0)
        {
            return false;
        }

        var prefix = pattern[..starIndex];
        var suffix = pattern[(starIndex + 1)..];

        if (origin.Length < prefix.Length + suffix.Length
            || !origin.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !origin.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var wildcardPart = origin.AsSpan(prefix.Length, origin.Length - prefix.Length - suffix.Length);
        return wildcardPart.Length > 0
            && !wildcardPart.Contains('/')
            && !wildcardPart.Contains('@');
    }
}
