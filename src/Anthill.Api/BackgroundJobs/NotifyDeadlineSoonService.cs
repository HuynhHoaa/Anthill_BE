using Anthill.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace Anthill.Api.BackgroundJobs;

/// <summary>
/// Quét những em chưa bắt đầu làm bài mà giờ của em sắp hết, rồi nhắc — §7, sự kiện <c>DeadlineSoon</c>.
/// </summary>
/// <remarks>
/// <para>
/// Phải là tiến trình nền chứ không phải đồng hồ ở trình duyệt: em chưa mở ứng dụng thì không có gì chạy
/// trên máy em, mà đúng em đó mới là người quên mất bài. Hạn giờ đọc từ database nên một lượt quét trễ chỉ
/// làm lời nhắc tới muộn hơn, không làm nó sai.
/// </para>
/// <para>
/// Cùng ba nguyên tắc với tiến trình tự nộp bài: mỗi vòng tự bọc lấy mình để một lỗi không giết cả tiến
/// trình, mỗi em một transaction dưới khoá dòng nên nhiều instance cùng chạy vẫn chỉ nhắc một lần, và mỗi
/// vòng mở một scope mới thay vì giữ một DbContext sống nhiều ngày.
/// </para>
/// </remarks>
public sealed class NotifyDeadlineSoonService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<DeadlineReminderOptions> _options;
    private readonly ILogger<NotifyDeadlineSoonService> _logger;

    public NotifyDeadlineSoonService(
        IServiceScopeFactory scopeFactory,
        IOptions<DeadlineReminderOptions> options,
        ILogger<NotifyDeadlineSoonService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (!options.Enabled)
        {
            _logger.LogInformation("Tiến trình nhắc sắp hết giờ đang tắt theo cấu hình.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, options.IntervalSeconds));

        _logger.LogInformation(
            "Tiến trình nhắc sắp hết giờ đã khởi động: quét mỗi {Interval} giây, biên {Margin} phút.",
            interval.TotalSeconds,
            options.MarginMinutes);

        using var timer = new PeriodicTimer(interval);

        // Quét ngay một lần trước khi vào nhịp: khởi động lại giữa buổi thi thì những em đã tới lúc nhắc
        // trong lúc dừng phải được nhắc ngay, không chờ hết một chu kỳ.
        do
        {
            await RunOnceAsync(options, stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));

        _logger.LogInformation("Tiến trình nhắc sắp hết giờ đã dừng.");
    }

    private async Task RunOnceAsync(DeadlineReminderOptions options, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var reminders = scope.ServiceProvider.GetRequiredService<IDeadlineReminderService>();

            var result = await reminders.SendDueRemindersAsync(
                options.BatchSize,
                options.MarginMinutes,
                stoppingToken);

            if (result.IsFailure)
            {
                _logger.LogWarning("Lượt quét nhắc sắp hết giờ không thành công: {Error}", result.Error);
                return;
            }

            if (result.ValueOrThrow > 0)
            {
                _logger.LogInformation("Đã nhắc {Count} học sinh về bài sắp đóng.", result.ValueOrThrow);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Ứng dụng đang tắt. Không phải lỗi.
        }
        catch (Exception exception)
        {
            // Bắt Exception trần có chủ đích: ngoại lệ thoát ra khỏi ExecuteAsync chấm dứt service vĩnh viễn
            // trong khi ứng dụng vẫn chạy, và từ lúc đó không em nào được nhắc nữa mà không ai nhận ra.
            _logger.LogError(exception, "Lượt quét nhắc sắp hết giờ gặp lỗi ngoài dự kiến, sẽ thử lại ở vòng sau.");
        }
    }

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
