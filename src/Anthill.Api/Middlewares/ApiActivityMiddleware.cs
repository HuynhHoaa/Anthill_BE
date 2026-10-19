using Anthill.Api.BackgroundJobs;

namespace Anthill.Api.Middlewares;

/// <summary>
/// Báo cho <see cref="ApiActivityTracker"/> mỗi khi có một yêu cầu đã xác thực, để các tiến trình quét database
/// biết có người đang dùng hệ thống.
/// </summary>
/// <remarks>
/// Đứng ngay SAU <c>UseAuthentication</c> (cần <c>HttpContext.User</c>) và TRƯỚC cổng đổi mật khẩu: một tài khoản
/// còn phải đổi mật khẩu vẫn là một người đang ngồi trước màn hình. Yêu cầu ẩn danh cố ý không tính, lý do ở
/// <see cref="ApiActivityTracker"/>. Chỉ đọc <c>User</c> chứ không đụng gì khác nên không thể làm hỏng yêu cầu.
/// </remarks>
public sealed class ApiActivityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiActivityTracker _activity;

    public ApiActivityMiddleware(RequestDelegate next, ApiActivityTracker activity)
    {
        _next = next;
        _activity = activity;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            _activity.MarkActive();
        }

        return _next(context);
    }
}
