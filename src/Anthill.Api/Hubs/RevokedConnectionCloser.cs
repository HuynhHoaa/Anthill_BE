using Microsoft.AspNetCore.SignalR;

namespace Anthill.Api.Hubs;

/// <summary>
/// Cắt một kết nối hub <b>sau khi</b> lời gọi đang chạy đã trả lỗi xuống client.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SessionRevalidationHubFilter"/> không gọi <c>Abort()</c> ngay: kể từ lúc đó SignalR bỏ mọi
/// message ghi ra kết nối, kể cả message báo lỗi của chính lời gọi — client chỉ thấy kết nối đứt mà
/// không biết vì sao. Bộ lọc giao kết nối cho đối tượng này rồi mới ném <c>HubException</c>.
/// </para>
/// <para>
/// Đối tượng sống trong scope DI của lời gọi hub. SignalR ghi lỗi của lời gọi xuống client trước, rồi
/// mới dispose scope đó — nên <see cref="Dispose"/> cắt kết nối đúng sau khi lý do đã được gửi đi.
/// Nếu một phiên bản SignalR sau này đảo thứ tự ấy thì chỉ mất câu lý do; kết nối vẫn bị cắt.
/// </para>
/// </remarks>
public sealed class RevokedConnectionCloser : IDisposable
{
    private HubCallerContext? _connection;

    public void CloseAfterReply(HubCallerContext connection) =>
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public void Dispose()
    {
        _connection?.Abort();
        _connection = null;
    }
}
