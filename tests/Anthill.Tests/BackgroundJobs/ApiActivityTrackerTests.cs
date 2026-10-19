using Anthill.Api.BackgroundJobs;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Anthill.Tests.BackgroundJobs;

/// <summary>
/// "Có người đang dùng hệ thống" quyết định lúc nào hai tiến trình quét database được nghỉ để database miễn phí
/// ngủ, nên ranh giới của nó phải đúng từng giây.
/// </summary>
public class ApiActivityTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);

    private static ApiActivityTracker NewTracker(int idleAfterSeconds, ManualClock clock) =>
        new(Options.Create(new JobIdleOptions { IdleAfterSeconds = idleAfterSeconds }), clock);

    [Fact]
    public void Chua_ai_dung_tu_luc_khoi_dong_thi_dang_vang()
    {
        var tracker = NewTracker(600, new ManualClock(Start));

        tracker.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Vua_co_nguoi_dung_thi_dang_hoat_dong()
    {
        var tracker = NewTracker(600, new ManualClock(Start));

        tracker.MarkActive();

        tracker.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Dung_den_het_khoang_vang_van_con_hoat_dong_qua_mot_giay_thi_vang()
    {
        var clock = new ManualClock(Start);
        var tracker = NewTracker(600, clock);
        tracker.MarkActive();

        clock.Advance(TimeSpan.FromSeconds(600));
        tracker.IsActive.Should().BeTrue("đúng mốc 600 giây vẫn chưa quá khoảng vắng");

        clock.Advance(TimeSpan.FromSeconds(1));
        tracker.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Co_nguoi_dung_lai_thi_khoang_vang_tinh_lai_tu_dau()
    {
        var clock = new ManualClock(Start);
        var tracker = NewTracker(600, clock);
        tracker.MarkActive();

        clock.Advance(TimeSpan.FromSeconds(500));
        tracker.MarkActive();
        clock.Advance(TimeSpan.FromSeconds(500));

        tracker.IsActive.Should().BeTrue("lần dùng cuối mới cách đây 500 giây");
    }

    [Fact]
    public void Khoang_vang_bang_0_thi_khong_bao_gio_vang()
    {
        var clock = new ManualClock(Start);
        var tracker = NewTracker(0, clock);

        tracker.IsActive.Should().BeTrue("chưa ai dùng nhưng việc nghỉ quét đã tắt");

        clock.Advance(TimeSpan.FromDays(30));
        tracker.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Khoang_vang_am_duoc_coi_nhu_0()
    {
        var tracker = NewTracker(-5, new ManualClock(Start));

        tracker.IdleAfter.Should().Be(TimeSpan.Zero);
        tracker.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Mac_dinh_la_muoi_phut()
    {
        new JobIdleOptions().IdleAfterSeconds.Should().Be(600);
    }

    /// <summary>Đồng hồ chỉnh tay, chỉ đủ cho phần đọc giờ mà <see cref="ApiActivityTracker"/> dùng.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now;

        public ManualClock(DateTimeOffset now) => _now = now;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
