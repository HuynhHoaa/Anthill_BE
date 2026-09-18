using System.Net;
using Anthill.Application.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Anthill.Api.Middlewares;

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
        catch (DbUpdateConcurrencyException exception)
        {
            // Concurrency token chặn một lần ghi lên bản đã cũ: request này đọc dòng, rồi có người
            // khác ghi dòng đó trước khi nó kịp lưu. Ví dụ điển hình: học sinh lưu hồ sơ đúng lúc quản
            // trị viên khoá tài khoản. Ghi tiếp là đè lên thứ người kia vừa quyết định mà không ai
            // được biết, nên thao tác bị huỷ nguyên vẹn và trả 409 — không phải 500, vì hàng rào đang
            // làm đúng việc của nó. Đứng trước nhánh dưới vì đây là lớp con của DbUpdateException.
            _logger.LogWarning(
                exception,
                "Xung đột ghi đồng thời ở {Method} {Path}.",
                context.Request.Method,
                context.Request.Path.Value);

            await WriteAsync(
                context,
                (int)HttpStatusCode.Conflict,
                "Dữ liệu này vừa được thay đổi ở nơi khác trong lúc bạn thao tác, nên thay đổi của bạn "
                + "chưa được lưu. Hãy tải lại trang rồi thử lại.",
                exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Hai thao tác ghi cùng lúc trên cùng một tài nguyên. Ví dụ rõ nhất: hai giáo viên
            // cùng bấm giao bài cho một lớp — cả hai đọc thấy "em này chưa có bài", cả hai cùng
            // ghi, và unique index IX_ExamAssignments_Exam_Student chặn người đến sau.
            //
            // Đây không phải sự cố hệ thống mà là kết quả đúng của một hàng rào đang làm việc, nên
            // trả 409 kèm lời chỉ dẫn thay vì 500 kèm "đã có lỗi xảy ra". Bắt ở đây thay vì trong
            // từng service vì mọi endpoint ghi đều có thể gặp, và tầng Application cố ý không biết
            // gì về EF Core.
            //
            // Trong thực tế mọi lần ghi đều đi qua UnitOfWork.SaveChangesAsync, và hàm đó giờ đã tự
            // bọc trường hợp này thành UniqueConstraintViolationException (nhánh catch ngay dưới) —
            // nhánh DbUpdateException thô ở đây chỉ còn là lưới đỡ cho một đường ghi giả định nào đó
            // không đi qua UnitOfWork.
            _logger.LogWarning(
                exception,
                "Va chạm ràng buộc duy nhất ở {Method} {Path}.",
                context.Request.Method,
                context.Request.Path.Value);

            await WriteAsync(
                context,
                (int)HttpStatusCode.Conflict,
                "Dữ liệu vừa được người khác thay đổi nên thao tác này không hoàn tất. "
                + "Hãy tải lại trang rồi thử lại.",
                exception);
        }
        catch (UniqueConstraintViolationException exception)
        {
            // Bản đã được UnitOfWork.SaveChangesAsync bọc lại từ DbUpdateException — xem nhánh phía
            // trên và remarks của UniqueConstraintViolationException. Cùng một câu trả lời 409, vì
            // đây là con đường bình thường mà hầu hết endpoint ghi đi qua; những endpoint biết cách
            // xử lý tốt hơn (ví dụ ExamAttemptService.CreateAttemptAsync — đọc lại và trả về đúng
            // lượt vừa được tạo) đã bắt riêng ngoại lệ này trước khi nó lọt tới đây.
            _logger.LogWarning(
                exception,
                "Va chạm ràng buộc duy nhất ở {Method} {Path}.",
                context.Request.Method,
                context.Request.Path.Value);

            await WriteAsync(
                context,
                (int)HttpStatusCode.Conflict,
                "Dữ liệu vừa được người khác thay đổi nên thao tác này không hoàn tất. "
                + "Hãy tải lại trang rồi thử lại.",
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

    /// <summary>
    /// Vi phạm ràng buộc duy nhất, nhận qua mã SQLSTATE <c>23505</c> của PostgreSQL.
    /// </summary>
    /// <remarks>
    /// Đọc mã lỗi thay vì so chuỗi thông điệp: thông điệp đổi theo ngôn ngữ và theo phiên bản
    /// máy chủ, còn SQLSTATE thì nằm trong chuẩn. Mọi <see cref="DbUpdateException"/> khác vẫn
    /// rơi xuống nhánh 500, vì chúng thật sự là sự cố chưa lường trước.
    /// </remarks>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

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
