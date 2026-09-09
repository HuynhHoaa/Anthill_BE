using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace EduQuiz.Api.Middlewares;

/// <summary>
/// Lưới đỡ cuối cùng: mọi exception chưa xử lý dừng lại ở đây và đi ra dưới đúng hình dạng
/// <c>{ message }</c> mà <c>apiClient.readErrorMessage</c> của frontend đang đọc (§1 hợp đồng).
/// </summary>
/// <remarks>
/// <para>
/// Lỗi <b>mong đợi</b> (không tìm thấy, không đủ quyền, trùng dữ liệu) không đi qua đây — chúng
/// đi qua <c>Result</c> và được controller dịch sang mã HTTP. Vào tới middleware này nghĩa là có
/// một sự cố thật, nên mọi trường hợp đều ghi log ở mức Error.
/// </para>
/// <para>
/// Cố ý <b>không</b> gọi <c>Response.Clear()</c>: lệnh đó xoá luôn các header mà middleware CORS
/// đã đặt ở vòng đi vào, khiến trình duyệt thấy một lỗi CORS khó hiểu thay vì thấy mã 500 kèm
/// thông điệp. Response chưa gửi đi thì gán lại status và body là đủ.
/// </para>
/// </remarks>
public sealed class ExceptionMiddleware
{
    /// <summary>
    /// Bộ mã hoá cho phép ký tự tiếng Việt đi thẳng ra JSON thay vì thành <c>ế</c>, nhưng vẫn
    /// giữ nguyên phần thoát của <c>&lt;</c>, <c>&gt;</c>, <c>&amp;</c> — thông điệp lỗi có thể chứa
    /// dữ liệu người dùng gõ vào.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private const string GenericMessage = "Máy chủ đang gặp sự cố. Vui lòng thử lại sau.";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Người dùng bấm F5 hoặc rời trang giữa chừng. Không phải lỗi, và cũng không còn ai
            // đọc response nữa — ghi một dòng Debug rồi thôi.
            _logger.LogDebug(
                "Client huỷ request {Method} {Path}.",
                context.Request.Method,
                context.Request.Path.Value);
        }
        catch (BadHttpRequestException exception)
        {
            // Body sai cú pháp, quá lớn, hoặc content-type lạ: lỗi của phía gửi, không phải sự cố server.
            _logger.LogWarning(
                exception,
                "Request không hợp lệ: {Method} {Path}.",
                context.Request.Method,
                context.Request.Path.Value);

            await WriteAsync(
                context,
                exception.StatusCode is >= 400 and < 500 ? exception.StatusCode : (int)HttpStatusCode.BadRequest,
                "Dữ liệu gửi lên không hợp lệ.",
                exception);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Lỗi chưa xử lý ở {Method} {Path}.",
                context.Request.Method,
                context.Request.Path.Value);

            await WriteAsync(context, (int)HttpStatusCode.InternalServerError, GenericMessage, exception);
        }
    }

    private async Task WriteAsync(HttpContext context, int statusCode, string message, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            // Header đã bay đi rồi, không sửa được nữa. Ghi log để lần sau còn lần ra được.
            _logger.LogWarning("Response đã bắt đầu gửi, không ghi được thân lỗi cho {Path}.", context.Request.Path.Value);
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new ErrorPayload
        {
            Message = message,

            // Chỉ ở Development. Trên production, stack trace là bản đồ chỉ đường cho người muốn
            // tấn công: nó lộ tên assembly, đường dẫn thư mục và cả thư viện đang dùng.
            Detail = _environment.IsDevelopment() ? exception.ToString() : null
        };

        // Cố ý dùng CancellationToken.None: request có thể đã bị huỷ, nhưng lúc này ta đang cố gửi
        // nốt thông điệp lỗi — huỷ giữa chừng chỉ để lại một response cụt.
        await context.Response.WriteAsJsonAsync(payload, JsonOptions, cancellationToken: CancellationToken.None);
    }

    /// <summary>
    /// Cùng hình dạng với <c>ApiErrorResponse</c> của tầng Application, thêm <c>detail</c> chỉ có ở
    /// Development. Khai báo riêng ở đây để middleware không phụ thuộc vào tầng Application.
    /// </summary>
    private sealed record ErrorPayload
    {
        public required string Message { get; init; }

        public string? Detail { get; init; }
    }
}
