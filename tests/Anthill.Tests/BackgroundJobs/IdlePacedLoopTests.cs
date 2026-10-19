using Anthill.Api.BackgroundJobs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anthill.Tests.BackgroundJobs;

/// <summary>
/// Nhịp quét của hai tiến trình nền: quét khi khởi động, quét đều khi có người dùng, nghỉ hẳn (không chạm database)
/// khi vắng, và quét lại ngay khi có người trở lại.
/// </summary>
/// <remarks>
/// Chạy bằng thời gian thật với nhịp vài chục mili giây. Phép kiểm "không quét" chỉ có thể đạt oan chứ không thể hỏng
/// oan khi máy chậm, còn phép kiểm "có quét" chờ có hạn nên chịu được máy chậm.
/// </remarks>
public class IdlePacedLoopTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(10);

    /// <summary>Chạy vòng quét ở nền, trả về bộ đếm số lượt quét cùng cách dừng nó.</summary>
    private sealed class RunningLoop : IAsyncDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _task;
        private int _scans;

        public RunningLoop(Func<bool> isActive)
        {
            _task = Task.Run(() => IdlePacedLoop.RunAsync(
                "thử",
                Interval,
                isActive,
                _ =>
                {
                    Interlocked.Increment(ref _scans);
                    return Task.CompletedTask;
                },
                NullLogger.Instance,
                _stop.Token,
                Poll));
        }

        public int Scans => Volatile.Read(ref _scans);

        public async Task StopAsync()
        {
            await _stop.CancelAsync();
            await _task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public async ValueTask DisposeAsync()
        {
            if (!_task.IsCompleted)
            {
                await StopAsync();
            }

            _stop.Dispose();
        }
    }

    /// <summary>Chờ tới khi điều kiện đúng, tối đa vài giây; trả về điều kiện có đúng không.</summary>
    private static async Task<bool> EventuallyAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(10);
        }

        return condition();
    }

    [Fact]
    public async Task Quet_mot_lan_khi_khoi_dong_ke_ca_khi_chua_ai_dung()
    {
        await using var loop = new RunningLoop(() => false);

        (await EventuallyAsync(() => loop.Scans >= 1)).Should().BeTrue();

        await Task.Delay(400);

        loop.Scans.Should().Be(1, "không có ai dùng thì ngoài lượt khởi động không được quét thêm");
    }

    [Fact]
    public async Task Co_nguoi_dung_thi_quet_deu_moi_nhip()
    {
        await using var loop = new RunningLoop(() => true);

        (await EventuallyAsync(() => loop.Scans >= 4)).Should().BeTrue("bốn lượt trong vài trăm mili giây");
    }

    [Fact]
    public async Task Het_nguoi_dung_thi_nghi_quet_va_co_nguoi_tro_lai_thi_quet_lai()
    {
        var active = true;
        await using var loop = new RunningLoop(() => Volatile.Read(ref active));

        (await EventuallyAsync(() => loop.Scans >= 3)).Should().BeTrue();

        Volatile.Write(ref active, false);
        await Task.Delay(150); // để lượt đang dở (nếu có) xong hẳn
        var whileResting = loop.Scans;
        await Task.Delay(400);

        loop.Scans.Should().Be(whileResting, "đang vắng thì không được chạm database");

        Volatile.Write(ref active, true);

        (await EventuallyAsync(() => loop.Scans > whileResting)).Should().BeTrue("có người trở lại thì quét lại");
    }

    [Fact]
    public async Task Nguoi_dung_tro_lai_thi_quet_ngay_khong_cho_het_khoang_quet()
    {
        // Khoảng quét một giờ: nếu vòng lặp đợi hết khoảng này sau lúc nghỉ thì phép kiểm sẽ quá hạn.
        var active = false;
        var scans = 0;

        using var stop = new CancellationTokenSource();
        var task = Task.Run(() => IdlePacedLoop.RunAsync(
            "thử",
            TimeSpan.FromHours(1),
            () => Volatile.Read(ref active),
            _ =>
            {
                Interlocked.Increment(ref scans);
                return Task.CompletedTask;
            },
            NullLogger.Instance,
            stop.Token,
            Poll));

        (await EventuallyAsync(() => Volatile.Read(ref scans) == 1)).Should().BeTrue("lượt khởi động");
        await Task.Delay(100);

        Volatile.Write(ref active, true);

        (await EventuallyAsync(() => Volatile.Read(ref scans) == 2)).Should().BeTrue();

        await stop.CancelAsync();
        await task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Khong_quet_chong_len_nhau_khi_mot_luot_quet_keo_dai()
    {
        var running = 0;
        var overlapped = false;
        var scans = 0;

        using var stop = new CancellationTokenSource();
        var task = Task.Run(() => IdlePacedLoop.RunAsync(
            "thử",
            Interval,
            () => true,
            async _ =>
            {
                if (Interlocked.Increment(ref running) > 1)
                {
                    overlapped = true;
                }

                await Task.Delay(120, CancellationToken.None);
                Interlocked.Decrement(ref running);
                Interlocked.Increment(ref scans);
            },
            NullLogger.Instance,
            stop.Token,
            Poll));

        (await EventuallyAsync(() => Volatile.Read(ref scans) >= 3)).Should().BeTrue();

        await stop.CancelAsync();
        await task.WaitAsync(TimeSpan.FromSeconds(5));

        overlapped.Should().BeFalse("mỗi lượt quét xong rồi mới tới lượt sau");
    }

    [Fact]
    public async Task Dung_ung_dung_thi_vong_quet_ket_thuc_khong_nem_loi()
    {
        var loop = new RunningLoop(() => true);
        (await EventuallyAsync(() => loop.Scans >= 1)).Should().BeTrue();

        var stopping = async () => await loop.StopAsync();

        await stopping.Should().NotThrowAsync();
    }
}
