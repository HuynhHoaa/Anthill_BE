using System.Diagnostics;

namespace Anthill.Api.BackgroundJobs;

/// <summary>
/// Nhịp quét chung của hai tiến trình nền phải tra database định kỳ: tự nộp bài và nhắc sắp hết giờ.
/// </summary>
/// <remarks>
/// <para>
/// Quét một lần ngay khi khởi động, không đợi ai: sau một lần triển khai giữa giờ thi, việc đã đến hạn trong lúc dừng
/// phải được xử lý liền. Sau đó quét mỗi <c>interval</c> chừng nào <c>isActive</c> còn trả true. Khi không ai dùng thì
/// chỉ hỏi lại <c>isActive</c> mỗi giây, một phép đọc bộ nhớ không chạm database, để database miễn phí được ngủ. Có
/// người dùng trở lại thì quét ngay ở nhịp hỏi kế tiếp, không đợi hết <c>interval</c>.
/// </para>
/// <para>
/// Hỏi mỗi giây thay vì chờ một tín hiệu đánh thức: đơn giản hơn hẳn (không có cuộc đua giữa lúc đi ngủ và lúc bị đánh
/// thức), và một lần đọc bộ nhớ mỗi giây không đáng kể.
/// </para>
/// <para>
/// Mỗi lần chuyển giữa nghỉ và quét được ghi một dòng log, để nhìn log trên máy chủ là biết database có đang được ngủ
/// hay không.
/// </para>
/// </remarks>
public static class IdlePacedLoop
{
    /// <summary>Nhịp hỏi lại "có người dùng chưa" khi đang nghỉ.</summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1);

    /// <param name="name">Tên tiến trình, chỉ để ghi log.</param>
    /// <param name="interval">Khoảng cách tối thiểu giữa hai lượt quét khi có người dùng.</param>
    /// <param name="isActive">Có người đang dùng hệ thống không.</param>
    /// <param name="scanOnce">Một lượt quét. Phải tự bắt lỗi của mình, như các tiến trình nền hiện có.</param>
    /// <param name="logger">Nơi ghi các lần chuyển giữa nghỉ và quét.</param>
    /// <param name="stoppingToken">Ứng dụng đang tắt.</param>
    /// <param name="pollInterval">Nhịp hỏi lại <paramref name="isActive"/>; mặc định một giây, đổi chỉ để chạy thử nhanh.</param>
    public static async Task RunAsync(
        string name,
        TimeSpan interval,
        Func<bool> isActive,
        Func<CancellationToken, Task> scanOnce,
        ILogger logger,
        CancellationToken stoppingToken,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(isActive);
        ArgumentNullException.ThrowIfNull(scanOnce);
        ArgumentNullException.ThrowIfNull(logger);

        await scanOnce(stoppingToken);

        var lastScanAt = Stopwatch.GetTimestamp();
        var resting = false;

        using var timer = new PeriodicTimer(pollInterval ?? DefaultPollInterval);

        while (await WaitForNextTickAsync(timer, stoppingToken))
        {
            if (!isActive())
            {
                if (!resting)
                {
                    resting = true;
                    logger.LogInformation("{Job}: không có ai dùng hệ thống, nghỉ quét để database được ngủ.", name);
                }

                continue;
            }

            if (resting)
            {
                resting = false;
                logger.LogInformation("{Job}: có người dùng trở lại, quét lại.", name);
            }
            else if (Stopwatch.GetElapsedTime(lastScanAt) < interval)
            {
                continue;
            }

            await scanOnce(stoppingToken);
            lastScanAt = Stopwatch.GetTimestamp();
        }
    }

    /// <summary>Chờ nhịp kế tiếp, trả về false khi ứng dụng đang tắt.</summary>
    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
