using EduQuiz.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace EduQuiz.Api.BackgroundJobs;

/// <summary>
/// Quét các lượt làm bài đã quá giờ và tự nộp chúng — §5 và §7.
/// </summary>
/// <remarks>
/// <para>
/// Tiến trình này <b>bắt buộc phải có</b>, không phải tiện ích. Học sinh tắt trình duyệt giữa giờ
/// thi thì không còn ai gọi submit; nếu chỉ trông vào client bấm giờ thì bài đó nằm mãi ở trạng
/// thái đang làm, không có điểm, và giáo viên không biết em đã làm tới đâu.
/// </para>
/// <para>
/// Ba điều quyết định ở đây:
/// </para>
/// <list type="number">
/// <item><b>Một lần quét lỗi không được giết cả tiến trình.</b> Ngoại lệ thoát ra khỏi
/// <c>ExecuteAsync</c> sẽ chấm dứt service vĩnh viễn và ứng dụng vẫn chạy như không có gì —
/// từ lúc đó không bài nào được tự nộp nữa, và không ai nhận ra cho tới khi có người thắc mắc.
/// Nên mỗi vòng tự bọc lấy mình, ghi log rồi đi tiếp.</item>
/// <item><b>Chịu được nhiều instance.</b> Việc chấm thật nằm trong <c>AutoSubmitExpiredAsync</c>,
/// nơi mỗi lượt được đọc dưới khoá ghi ở mức dòng và kiểm lại trạng thái sau khi có khoá. Hai
/// tiến trình cùng bắt được một bài thì người đến sau thấy nó đã <c>graded</c> và bỏ qua.</item>
/// <item><b>Hạn giờ đọc từ database, không từ đồng hồ của vòng lặp.</b> Quét trễ vài chục giây
/// không làm sai điểm: bài vẫn được chấm theo đúng <c>ExpiresAt</c> của nó.</item>
/// </list>
/// </remarks>
public sealed class AutoSubmitExpiredAttemptsService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AutoSubmitOptions> _options;
    private readonly ILogger<AutoSubmitExpiredAttemptsService> _logger;

    public AutoSubmitExpiredAttemptsService(
        IServiceScopeFactory scopeFactory,
        IOptions<AutoSubmitOptions> options,
        ILogger<AutoSubmitExpiredAttemptsService> logger)
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
            _logger.LogInformation("Tiến trình tự nộp bài đang tắt theo cấu hình.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, options.IntervalSeconds));

        _logger.LogInformation(
            "Tiến trình tự nộp bài đã khởi động: quét mỗi {Interval} giây, tối đa {BatchSize} bài mỗi lượt.",
            interval.TotalSeconds,
            options.BatchSize);

        using var timer = new PeriodicTimer(interval);

        // Quét ngay một lần trước khi vào nhịp: khởi động lại sau một lần triển khai giữa giờ thi
        // thì những bài đã quá hạn trong lúc dừng phải được xử lý ngay, không chờ hết một chu kỳ.
        do
        {
            await RunOnceAsync(options.BatchSize, stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));

        _logger.LogInformation("Tiến trình tự nộp bài đã dừng.");
    }

    /// <summary>Một vòng quét. Tự bọc lấy mình để lỗi không thoát lên vòng lặp.</summary>
    private async Task RunOnceAsync(int batchSize, CancellationToken stoppingToken)
    {
        try
        {
            // BackgroundService là singleton còn service nghiệp vụ đăng ký scoped, nên mỗi vòng
            // phải tự mở một scope. Giữ một scope cho cả vòng đời tiến trình nghĩa là giữ một
            // DbContext sống nhiều ngày, với change tracker phình ra không giới hạn.
            using var scope = _scopeFactory.CreateScope();
            var attempts = scope.ServiceProvider.GetRequiredService<IExamAttemptService>();

            var result = await attempts.AutoSubmitExpiredAsync(batchSize, stoppingToken);

            if (result.IsFailure)
            {
                _logger.LogWarning("Lượt quét tự nộp bài không thành công: {Error}", result.Error);
                return;
            }

            if (result.ValueOrThrow > 0)
            {
                _logger.LogInformation("Đã tự nộp {Count} bài quá hạn.", result.ValueOrThrow);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Ứng dụng đang tắt. Không phải lỗi.
        }
        catch (Exception exception)
        {
            // Bắt Exception trần là có chủ đích: bất kỳ lỗi nào thoát ra khỏi ExecuteAsync đều
            // chấm dứt service vĩnh viễn trong khi ứng dụng vẫn chạy bình thường. Một lần mất kết
            // nối database không được phép làm im lặng cả cơ chế tự nộp bài.
            _logger.LogError(exception, "Lượt quét tự nộp bài gặp lỗi ngoài dự kiến, sẽ thử lại ở vòng sau.");
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
