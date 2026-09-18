using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Anthill.Api.Security;
using Anthill.Application.Common;
using Microsoft.AspNetCore.Authorization;

namespace Anthill.Api.Middlewares;

/// <summary>
/// Cổng chặn cho tài khoản đang mang <c>MustChangePassword = true</c> (§5): trong lúc cờ này còn
/// bật, chỉ ba endpoint gắn <see cref="AllowWhileMustChangePasswordAttribute"/> được đi tiếp — mọi
/// endpoint nghiệp vụ khác nhận 403 ngay tại đây, trước khi chạm tới controller.
/// </summary>
/// <remarks>
/// <para>
/// Đứng SAU <c>UseAuthentication</c> và TRƯỚC <c>UseAuthorization</c> trong <c>Program.cs</c>: cần
/// <c>HttpContext.User</c> đã có (do <c>OnTokenValidated</c> gán) để biết ai đang gọi, và endpoint đã
/// được chọn (do <c>UseRouting</c> chạy trước cả hai) để đọc thuộc tính trên action method.
/// </para>
/// <para>
/// <c>OnTokenValidated</c> ghi cờ này vào <c>HttpContext.Items</c> ngay khi xác thực thành công —
/// dùng lại đúng một truy vấn theo khoá chính đã chạy cho phép kiểm phiên (<c>SessionValidation</c>),
/// không tra database thêm lần nữa. Thiếu mục này trong <c>Items</c> (token hỏng, tài khoản bị chặn
/// vừa lọt qua nhờ <c>ReportsBlockedAccountAttribute</c>...) coi như không bật cờ — nhánh đó đã có
/// chỗ khác chịu trách nhiệm trả lời, không phải middleware này.
/// </para>
/// <para>
/// Bỏ qua hẳn đường dẫn <c>/hubs</c>: kết nối SignalR không nằm trong phạm vi ba endpoint mà báo cáo
/// audit liệt kê, và giao diện học sinh còn cờ này không bao giờ mở kết nối đó (layout không render
/// tới nơi có hub).
/// </para>
/// </remarks>
public sealed class MustChangePasswordMiddleware
{
    /// <summary>Khoá trong <c>HttpContext.Items</c> mà <c>OnTokenValidated</c> ghi vào.</summary>
    public const string MustChangePasswordItemKey = "Anthill.MustChangePassword";

    private const string BlockedMessage = "Bạn cần đổi mật khẩu trước khi tiếp tục sử dụng hệ thống.";

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || context.Items[MustChangePasswordItemKey] is not true
            || context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var endpoint = context.GetEndpoint();

        // [AllowAnonymous] nghĩa là endpoint này đứng trước mọi khái niệm "tài khoản đang đăng
        // nhập" (đăng nhập, refresh) — cờ MustChangePassword của một token vô tình còn đính kèm
        // không liên quan gì tới nó.
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || endpoint?.Metadata.GetMetadata<AllowWhileMustChangePasswordAttribute>() is not null)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(ApiErrorResponse.Of(BlockedMessage), ResponseJsonOptions);
    }
}
