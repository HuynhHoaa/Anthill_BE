using Anthill.Application.Interfaces.Realtime;

namespace Anthill.Api.Realtime;

/// <summary>
/// Sổ ghi các kết nối hub đang mở, theo từng tài khoản.
/// </summary>
/// <remarks>
/// <para>
/// <c>NotificationHub</c> ghi vào ở <c>OnConnectedAsync</c> và xoá ở <c>OnDisconnectedAsync</c>; các lối
/// xoay <c>SecurityStamp</c> gọi <see cref="CloseConnectionsOf"/> qua <see cref="IUserConnectionCloser"/>.
/// Không có sổ này thì một kết nối mở trước lúc khoá vẫn nhận thông báo cá nhân tới lần gọi hub kế tiếp,
/// hoặc tới khi token hết hạn — tối đa 120 phút.
/// </para>
/// <para>
/// Sổ không biết gì về SignalR: mỗi kết nối chỉ là một id kèm hàm cắt, nên kiểm được bằng unit test mà
/// không cần dựng hub.
/// </para>
/// <para>
/// <b>Giới hạn: chỉ đúng khi API chạy một tiến trình.</b> Sổ nằm trong bộ nhớ, nên tiến trình này chỉ
/// cắt được kết nối do chính nó giữ. Chạy nhiều bản sau bộ cân bằng tải (kèm backplane Redis cho SignalR)
/// thì lệnh cắt phải được phát qua backplane tới mọi bản; tới lúc đó, kết nối ở bản khác vẫn bị chặn ở
/// lần gọi hub kế tiếp và khi token hết hạn, như trước khi có sổ này.
/// </para>
/// </remarks>
public sealed class UserConnectionRegistry : IUserConnectionCloser
{
    // Một khoá chung thay vì ConcurrentDictionary lồng nhau: xoá mục rỗng của một tài khoản chen đúng
    // lúc có kết nối mới ghi vào là kiểu lỗi làm rơi mất một kết nối khỏi sổ — tức là đúng kết nối không
    // bao giờ bị cắt. Mỗi thao tác chỉ vài phép tra từ điển, và kết nối mở hay đóng thưa hơn request.
    private readonly Lock _gate = new();
    private readonly Dictionary<int, Dictionary<string, Action>> _connections = [];

    /// <summary>Ghi nhận một kết nối vừa mở.</summary>
    /// <param name="userId">Tài khoản đang giữ kết nối, đọc từ claim <c>sub</c>.</param>
    /// <param name="connectionId">Id kết nối do SignalR cấp.</param>
    /// <param name="abort">Hàm cắt kết nối, gọi được từ bất kỳ luồng nào.</param>
    public void Register(int userId, string connectionId, Action abort)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionId);
        ArgumentNullException.ThrowIfNull(abort);

        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var byConnection))
            {
                byConnection = new Dictionary<string, Action>(StringComparer.Ordinal);
                _connections[userId] = byConnection;
            }

            byConnection[connectionId] = abort;
        }
    }

    /// <summary>Xoá một kết nối đã đóng. Kết nối không có trong sổ thì bỏ qua.</summary>
    public void Unregister(int userId, string connectionId)
    {
        lock (_gate)
        {
            if (_connections.TryGetValue(userId, out var byConnection)
                && byConnection.Remove(connectionId)
                && byConnection.Count == 0)
            {
                _connections.Remove(userId);
            }
        }
    }

    /// <inheritdoc />
    public int CloseConnectionsOf(int userId)
    {
        Action[] aborts;

        lock (_gate)
        {
            if (!_connections.Remove(userId, out var byConnection))
            {
                return 0;
            }

            aborts = [.. byConnection.Values];
        }

        // Gọi ngoài khoá: cắt kết nối kéo theo OnDisconnectedAsync, và nó sẽ gọi Unregister — giữ khoá ở
        // đây thì một bản cài đặt đồng bộ của Abort là tự chặn chính mình.
        foreach (var abort in aborts)
        {
            abort();
        }

        return aborts.Length;
    }

    /// <summary>Số kết nối đang ghi cho một tài khoản.</summary>
    public int CountOf(int userId)
    {
        lock (_gate)
        {
            return _connections.TryGetValue(userId, out var byConnection) ? byConnection.Count : 0;
        }
    }
}
