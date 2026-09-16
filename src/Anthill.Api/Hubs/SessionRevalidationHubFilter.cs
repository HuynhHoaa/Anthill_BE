using Anthill.Api.Security;
using Anthill.Application.Common;
using Anthill.Application.Interfaces.Security;
using Microsoft.AspNetCore.SignalR;

namespace Anthill.Api.Hubs;

/// <summary>
/// Kiểm lại phiên trước <b>mỗi</b> lần client gọi một phương thức hub.
/// </summary>
/// <remarks>
/// <para>
/// <c>OnTokenValidated</c> chỉ chạy trên request HTTP: negotiate, request mở WebSocket, và từng lượt
/// long polling. Khi WebSocket đã mở thì mọi lời gọi hub đi trên cùng một kết nối và dùng lại
/// <c>ClaimsPrincipal</c> của lúc kết nối — khoá tài khoản, đưa vào diện xoá hay đổi mật khẩu sau đó
/// không chạm được tới nó. Thiếu bộ lọc này, một kết nối mở trước lúc bị khoá vẫn gọi
/// <c>JoinExamGroup</c> được như thường.
/// </para>
/// <para>
/// Token không còn hợp lệ thì cắt kết nối rồi ném <see cref="HubException"/> kèm lý do.
/// <c>HubException</c> là loại lỗi duy nhất SignalR chuyển nguyên thông điệp xuống client khi
/// <c>EnableDetailedErrors</c> tắt. Cắt kết nối để client không gọi tiếp được nữa: muốn dùng lại thì
/// phải negotiate, và lần đó <c>OnTokenValidated</c> trả 401.
/// </para>
/// <para>
/// Đánh đổi: thêm một truy vấn theo khoá chính cho mỗi lời gọi hub, đúng mức giá đã chấp nhận cho REST.
/// Hub chỉ có ba phương thức và client gọi chúng thưa.
/// </para>
/// <para>
/// Bộ lọc này không che được sự kiện server <b>đẩy</b> xuống một kết nối đã mở: việc gửi đi không đi
/// qua phương thức hub nào. Phần đó do <c>UserConnectionRegistry</c> lo — nó cắt kết nối ngay lúc stamp
/// xoay. Bộ lọc vẫn giữ vì sổ kết nối chỉ biết kết nối của tiến trình mình (xem lớp đó).
/// </para>
/// </remarks>
public sealed class SessionRevalidationHubFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);

        var connection = invocationContext.Context;
        var user = connection.User;

        // Cùng scope với hub sắp chạy. AccountSessionReader đọc AsNoTracking nên không để lại entity
        // User nào trong DbContext mà phương thức hub dùng sau đó.
        var reader = invocationContext.ServiceProvider.GetRequiredService<IAccountSessionReader>();
        var verdict = await SessionValidation.CheckAsync(
            user?.FindFirst(CurrentUser.UserIdClaimType)?.Value,
            user?.FindFirst(CurrentUser.SecurityStampClaimType)?.Value,
            reader,
            connection.ConnectionAborted);

        if (verdict == SessionVerdict.Valid)
        {
            return await next(invocationContext);
        }

        // Không Abort() ngay ở đây: sau Abort() SignalR bỏ mọi message ghi ra, kể cả lỗi mang lý do
        // của lời gọi này. RevokedConnectionCloser cắt kết nối khi scope của lời gọi đóng lại, tức là
        // sau khi lỗi đã tới client.
        invocationContext.ServiceProvider.GetRequiredService<RevokedConnectionCloser>().CloseAfterReply(connection);
        throw new HubException(SessionValidation.MessageFor(verdict));
    }
}
