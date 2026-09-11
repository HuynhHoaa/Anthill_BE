using System.Diagnostics;

namespace Anthill.Api.Middlewares;

/// <summary>
/// Một dòng log cho mỗi request: phương thức, đường dẫn, mã trạng thái, thời gian xử lý.
/// </summary>
/// <remarks>
/// Chỉ ghi những gì an toàn: <b>không</b> log body, không log header <c>Authorization</c>, không log
/// query <c>access_token</c> mà SignalR gửi kèm (§7) — đó đều là thông tin đăng nhập, lọt vào file
/// log là lọt ra ngoài.
/// </remarks>
public sealed class RequestLoggingMiddleware
{
    /// <summary>Đường dẫn hạ tầng, gọi liên tục và không mang thông tin gì khi đọc log.</summary>
    private static readonly string[] IgnoredPrefixes = ["/swagger", "/health", "/favicon.ico"];

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (IgnoredPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        Exception? failure = null;

        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            // Middleware này nằm BÊN TRONG ExceptionMiddleware, nên lúc khối finally chạy thì mã trạng
            // thái vẫn còn là 200 — exception mới đang trên đường đi ra. Ghi nhận ở đây để dòng log
            // không nói dối rằng request đã thành công. Phần stack trace do ExceptionMiddleware lo.
            failure = exception;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            if (failure is not null)
            {
                _logger.LogWarning(
                    "{Method} {Path} → {Exception} ({Elapsed} ms)",
                    context.Request.Method,
                    path,
                    failure.GetType().Name,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                // 5xx đáng chú ý hơn hẳn phần còn lại; tách mức log để lọc nhanh khi có sự cố.
                var level = context.Response.StatusCode >= 500 ? LogLevel.Warning : LogLevel.Information;

                _logger.Log(
                    level,
                    "{Method} {Path} → {StatusCode} ({Elapsed} ms)",
                    context.Request.Method,
                    path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
