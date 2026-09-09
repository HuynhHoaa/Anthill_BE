using Microsoft.AspNetCore.SignalR;

namespace EduQuiz.Api.Security;

/// <summary>
/// Dạy SignalR biết "một người dùng" là ai, để <c>Clients.User(id)</c> gửi được tới mọi kết nối
/// của cùng một tài khoản (nhiều tab, điện thoại và máy tính).
/// </summary>
/// <remarks>
/// Bản mặc định của SignalR đọc <c>ClaimTypes.NameIdentifier</c>. Token của EduQuiz đặt danh tính
/// ở claim <c>sub</c> và cấu hình JWT cố ý tắt ánh xạ claim, nên nếu không thay bản này thì
/// <c>Context.UserIdentifier</c> luôn NULL và mọi lời gọi <c>Clients.User(...)</c> rơi vào hư không —
/// im lặng, không lỗi.
/// </remarks>
public sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(CurrentUser.UserIdClaimType)?.Value;
}
