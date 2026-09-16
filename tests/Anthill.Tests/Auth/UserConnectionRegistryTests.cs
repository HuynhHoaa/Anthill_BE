using Anthill.Api.Realtime;
using FluentAssertions;

namespace Anthill.Tests.Auth;

/// <summary>
/// Sổ kết nối là thứ biến "khoá tài khoản" thành "kết nối đang mở đóng ngay". Một kết nối rơi khỏi sổ
/// là một kết nối tiếp tục nhận thông báo của tài khoản đã bị khoá.
/// </summary>
public class UserConnectionRegistryTests
{
    private sealed class FakeConnection
    {
        public int AbortCount { get; private set; }

        public void Abort() => AbortCount++;
    }

    [Fact]
    public void Cat_moi_ket_noi_cua_dung_tai_khoan_va_khong_dung_tai_khoan_khac()
    {
        var registry = new UserConnectionRegistry();
        var tab1 = new FakeConnection();
        var tab2 = new FakeConnection();
        var other = new FakeConnection();
        registry.Register(7, "c1", tab1.Abort);
        registry.Register(7, "c2", tab2.Abort);
        registry.Register(8, "c3", other.Abort);

        registry.CloseConnectionsOf(7).Should().Be(2);

        tab1.AbortCount.Should().Be(1);
        tab2.AbortCount.Should().Be(1);
        other.AbortCount.Should().Be(0);
        registry.CountOf(7).Should().Be(0);
        registry.CountOf(8).Should().Be(1);
    }

    [Fact]
    public void Tai_khoan_khong_co_ket_noi_nao_thi_khong_cat_gi()
    {
        new UserConnectionRegistry().CloseConnectionsOf(42).Should().Be(0);
    }

    [Fact]
    public void Ket_noi_da_dong_khong_bi_cat_lai()
    {
        var registry = new UserConnectionRegistry();
        var closed = new FakeConnection();
        var open = new FakeConnection();
        registry.Register(7, "c1", closed.Abort);
        registry.Register(7, "c2", open.Abort);

        registry.Unregister(7, "c1");

        registry.CloseConnectionsOf(7).Should().Be(1);
        closed.AbortCount.Should().Be(0);
        open.AbortCount.Should().Be(1);
    }

    [Fact]
    public void Cat_hai_lan_lien_tiep_chi_goi_abort_mot_lan()
    {
        // Khoá rồi cấp lại mật khẩu ngay sau đó: lần cắt thứ hai không được chạm lại kết nối đã cắt.
        var registry = new UserConnectionRegistry();
        var connection = new FakeConnection();
        registry.Register(7, "c1", connection.Abort);

        registry.CloseConnectionsOf(7);
        registry.CloseConnectionsOf(7).Should().Be(0);

        connection.AbortCount.Should().Be(1);
    }

    [Fact]
    public void Ket_noi_mo_sau_lan_cat_duoc_ghi_lai_binh_thuong()
    {
        // Đăng nhập lại sau khi được mở khoá thì kết nối mới phải nằm trong sổ, để lần khoá sau cắt được.
        var registry = new UserConnectionRegistry();
        registry.Register(7, "c1", new FakeConnection().Abort);
        registry.CloseConnectionsOf(7);

        var reconnected = new FakeConnection();
        registry.Register(7, "c2", reconnected.Abort);

        registry.CloseConnectionsOf(7).Should().Be(1);
        reconnected.AbortCount.Should().Be(1);
    }

    [Fact]
    public async Task Abort_goi_nguoc_Unregister_khong_tu_chan_minh()
    {
        // SignalR cắt kết nối rồi gọi OnDisconnectedAsync, và hàm đó xoá kết nối khỏi sổ. Nếu lần cắt còn
        // giữ khoá của sổ thì một Abort chạy đồng bộ sẽ treo ngay tại đây.
        var registry = new UserConnectionRegistry();
        var aborted = 0;
        registry.Register(7, "c1", () =>
        {
            aborted++;
            registry.Unregister(7, "c1");
        });

        var close = Task.Run(() => registry.CloseConnectionsOf(7));

        // Hết 5 giây mà chưa xong thì WaitAsync ném TimeoutException — đúng dấu hiệu của tự chặn chính mình.
        await close.WaitAsync(TimeSpan.FromSeconds(5));
        aborted.Should().Be(1);
    }

    [Fact]
    public async Task Ghi_xoa_dong_thoi_khong_lam_roi_ket_noi_nao()
    {
        // Tám luồng cùng mở rồi đóng hàng nghìn kết nối của một tài khoản — mục của tài khoản đó liên tục
        // rỗng đi rồi được tạo lại — và mỗi luồng kết thúc bằng một kết nối không đóng. Cùng lúc, một luồng
        // khác cắt liên tục một tài khoản thứ hai. Kết nối nào rơi khỏi sổ trong mớ chen lấn đó là kết nối
        // lần khoá sau không cắt được.
        var registry = new UserConnectionRegistry();
        const int workers = 8;
        var survivors = Enumerable.Range(0, workers).Select(_ => new FakeConnection()).ToArray();
        using var stop = new CancellationTokenSource();

        var closer = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                registry.Register(8, "other", () => { });
                registry.CloseConnectionsOf(8);
            }
        });

        var churn = Enumerable.Range(0, workers).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 2000; i++)
            {
                var id = $"w{worker}-{i}";
                registry.Register(7, id, () => { });
                registry.Unregister(7, id);
            }

            registry.Register(7, $"survivor-{worker}", survivors[worker].Abort);
        })).ToArray();

        await Task.WhenAll(churn);
        await stop.CancelAsync();
        await closer;

        registry.CountOf(7).Should().Be(workers);
        registry.CloseConnectionsOf(7).Should().Be(workers);
        survivors.Should().OnlyContain(connection => connection.AbortCount == 1);
    }
}
