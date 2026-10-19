using Microsoft.Extensions.Options;

namespace Anthill.Api.BackgroundJobs;

/// <summary>
/// Ghi lại lần cuối có người dùng hệ thống, để các tiến trình nền biết lúc nào nên nghỉ quét.
/// </summary>
/// <remarks>
/// <para>
/// "Có người dùng" nghĩa là một yêu cầu <b>đã xác thực</b>, xem <see cref="Middlewares.ApiActivityMiddleware"/>. Cố ý
/// không tính yêu cầu ẩn danh: máy dò lỗ hổng và trình thu thập gọi các đường dẫn công khai suốt ngày, còn
/// <c>/health</c> do lời gọi đánh thức của frontend và các dịch vụ giữ thức gọi. Tính chúng thì lúc nào cũng "có
/// người dùng" và việc nghỉ quét thành vô nghĩa.
/// </para>
/// <para>
/// Một thể hiện cho cả tiến trình, chỉ giữ một con số trong bộ nhớ. Ghi từ mọi thread của request, đọc từ các
/// tiến trình nền, nên dùng <see cref="Interlocked"/> để cả hai phía thấy giá trị đầy đủ.
/// </para>
/// </remarks>
public sealed class ApiActivityTracker
{
    private readonly TimeSpan _idleAfter;
    private readonly TimeProvider _clock;

    /// <summary>Ticks UTC của lần cuối có người dùng. 0 nghĩa là chưa ai dùng từ lúc API khởi động.</summary>
    private long _lastActivityTicks;

    public ApiActivityTracker(IOptions<JobIdleOptions> options, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _idleAfter = TimeSpan.FromSeconds(Math.Max(0, options.Value.IdleAfterSeconds));
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Khoảng không ai dùng thì coi là vắng. Bằng 0 nghĩa là không bao giờ vắng.</summary>
    public TimeSpan IdleAfter => _idleAfter;

    /// <summary>Gọi mỗi khi có một yêu cầu đã xác thực. Rẻ: một phép ghi duy nhất.</summary>
    public void MarkActive() => Interlocked.Exchange(ref _lastActivityTicks, _clock.GetUtcNow().UtcTicks);

    /// <summary>Có người đang dùng không, tức lần cuối chưa quá <see cref="IdleAfter"/>.</summary>
    public bool IsActive
    {
        get
        {
            if (_idleAfter == TimeSpan.Zero)
            {
                return true;
            }

            var last = Interlocked.Read(ref _lastActivityTicks);
            return last != 0 && _clock.GetUtcNow().UtcTicks - last <= _idleAfter.Ticks;
        }
    }
}
