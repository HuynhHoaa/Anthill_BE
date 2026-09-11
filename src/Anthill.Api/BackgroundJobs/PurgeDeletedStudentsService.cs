using Anthill.Application.Common;
using Anthill.Application.Interfaces.Services;

namespace Anthill.Api.BackgroundJobs;

/// <summary>
/// Xoá dữ liệu cá nhân của những tài khoản đã hết thời gian chờ.
/// </summary>
/// <remarks>
/// <para>
/// Quét <b>mỗi sáu tiếng</b> chứ không phải mỗi ba mươi giây như tiến trình tự nộp bài, vì hai
/// việc có tính cấp bách khác hẳn nhau: nộp bài trễ một phút là học sinh mất bài, còn xoá dữ liệu
/// trễ vài tiếng so với mốc sáu mươi ngày thì không ai nhận ra.
/// </para>
/// <para>
/// Chậm lại còn là một lớp an toàn: ẩn danh hoá không hoàn tác được, nên quét thưa cho người ta
/// thêm thời gian phát hiện một quyết định sai.
/// </para>
/// </remarks>
public sealed class PurgeDeletedStudentsService : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(6);

    /// <summary>Đủ lớn cho một trường cỡ vừa, đủ nhỏ để không thành một transaction khổng lồ.</summary>
    private const int BatchSize = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PurgeDeletedStudentsService> _logger;

    public PurgeDeletedStudentsService(
        IServiceScopeFactory scopeFactory,
        ILogger<PurgeDeletedStudentsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Tiến trình xoá dữ liệu học sinh đã khởi động: quét mỗi {Hours} giờ, thời gian chờ {Days} ngày.",
            ScanInterval.TotalHours,
            StudentAnonymizer.RetentionDays);

        using var timer = new PeriodicTimer(ScanInterval);

        // Quét ngay một lần: sau một lần triển khai, những tài khoản đến hạn trong lúc dừng phải
        // được xử lý mà không phải chờ trọn sáu tiếng.
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var students = scope.ServiceProvider.GetRequiredService<IStudentService>();

            var result = await students.PurgeDueAccountsAsync(BatchSize, stoppingToken);

            if (result.IsFailure)
            {
                _logger.LogWarning("Lượt quét xoá dữ liệu không thành công: {Error}", result.Error);
                return;
            }

            if (result.ValueOrThrow > 0)
            {
                _logger.LogInformation("Đã xoá dữ liệu cá nhân của {Count} tài khoản.", result.ValueOrThrow);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Ứng dụng đang tắt.
        }
        catch (Exception exception)
        {
            // Cùng lý do với tiến trình tự nộp bài: ngoại lệ thoát ra khỏi ExecuteAsync sẽ chấm dứt
            // service vĩnh viễn trong khi ứng dụng vẫn chạy, và từ đó không tài khoản nào được xoá
            // nữa mà không ai biết.
            _logger.LogError(exception, "Lượt quét xoá dữ liệu gặp lỗi ngoài dự kiến, sẽ thử lại ở vòng sau.");
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
