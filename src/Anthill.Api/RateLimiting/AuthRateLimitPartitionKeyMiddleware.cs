using System.Text.Json;

namespace Anthill.Api.RateLimiting;

/// <summary>
/// Tính khoá phân vùng cho RateLimiter của hai endpoint xác thực, ghi vào <c>HttpContext.Items</c>
/// trước khi <c>UseRateLimiter</c> chạy tới (§H20).
/// </summary>
/// <remarks>
/// Bộ chọn khoá phân vùng của <c>AddPolicy</c> bắt buộc là một hàm <b>đồng bộ</b>, nên không đọc
/// được thân request (đăng nhập gửi định danh trong body JSON, không phải claim) một cách bất đồng
/// bộ ngay tại đó. Middleware này đứng trước, làm phần đọc bất đồng bộ một lần, rồi để bộ chọn khoá
/// chỉ việc đọc lại một chuỗi có sẵn trong <c>Items</c>.
/// <para>
/// Khoá phân vùng là <c>{ip}|{định danh}</c>: kết hợp cả hai để (a) một địa chỉ dò nhiều tài khoản
/// khác nhau và (b) một tài khoản bị dò từ nhiều địa chỉ khác nhau đều vẫn bị đếm riêng theo đúng
/// cặp của nó, không cộng dồn nhầm sang tài khoản hay địa chỉ khác.
/// </para>
/// </remarks>
public sealed class AuthRateLimitPartitionKeyMiddleware
{
    /// <summary>Khoá trong <c>HttpContext.Items</c> mà policy "auth" của RateLimiter đọc lại.</summary>
    public const string ItemKey = "AuthRateLimitPartitionKey";

    private static readonly string[] LimitedPaths =
    [
        "/api/auth/login",
        "/api/auth/change-password"
    ];

    private readonly RequestDelegate _next;

    public AuthRateLimitPartitionKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method) && MatchesLimitedPath(context.Request.Path))
        {
            // Đổi mật khẩu đứng sau [Authorize]: middleware này chạy sau UseAuthentication nên claim
            // đã có sẵn, không cần đọc body. Đăng nhập thì ngược lại — chưa có claim nào, định danh
            // chỉ nằm trong body.
            var identifier = context.User.Identity?.IsAuthenticated == true
                ? context.User.FindFirst("sub")?.Value
                : await ReadLoginIdentifierAsync(context.Request);

            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "khong-ro-ip";

            context.Items[ItemKey] = $"{ip}|{Normalize(identifier)}";
        }

        await _next(context);
    }

    private static bool MatchesLimitedPath(PathString path) =>
        Array.Exists(LimitedPaths, limited => path.StartsWithSegments(limited, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Đọc trường "username" thẳng từ thân JSON thay vì chờ model binding — middleware này chạy
    /// trước khi MVC đọc thân request, nên phải bật buffering và trả con trỏ về đầu cho bước đó.
    /// </summary>
    private static async Task<string?> ReadLoginIdentifierAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
            return document.RootElement.TryGetProperty("username", out var usernameProperty)
                   && usernameProperty.ValueKind == JsonValueKind.String
                ? usernameProperty.GetString()
                : null;
        }
        catch (JsonException)
        {
            // Thân request sai định dạng: ValidationFilter phía sau sẽ trả lỗi cụ thể. Ở đây chỉ
            // không phân vùng được theo định danh — request vẫn bị đếm theo IP.
            return null;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "?" : value.Trim().ToLowerInvariant();
}
