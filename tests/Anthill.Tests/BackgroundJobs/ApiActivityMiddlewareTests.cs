using System.Security.Claims;
using Anthill.Api.BackgroundJobs;
using Anthill.Api.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Anthill.Tests.BackgroundJobs;

/// <summary>
/// Chỉ yêu cầu đã xác thực mới tính là "có người dùng". Yêu cầu ẩn danh (máy dò lỗ hổng, lời gọi giữ thức, lời gọi
/// đánh thức của frontend) mà tính thì hệ thống lúc nào cũng "có người" và database miễn phí không bao giờ được ngủ.
/// </summary>
public class ApiActivityMiddlewareTests
{
    private static ApiActivityTracker NewTracker() =>
        new(Options.Create(new JobIdleOptions { IdleAfterSeconds = 600 }));

    private static DefaultHttpContext AuthenticatedContext() => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7")], "Bearer"))
    };

    [Fact]
    public async Task Yeu_cau_da_xac_thuc_danh_dau_co_nguoi_dung()
    {
        var tracker = NewTracker();
        var middleware = new ApiActivityMiddleware(_ => Task.CompletedTask, tracker);

        await middleware.InvokeAsync(AuthenticatedContext());

        tracker.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Yeu_cau_an_danh_khong_danh_dau()
    {
        var tracker = NewTracker();
        var middleware = new ApiActivityMiddleware(_ => Task.CompletedTask, tracker);

        await middleware.InvokeAsync(new DefaultHttpContext());

        tracker.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Duong_kiem_tra_song_khong_can_token_khong_danh_dau()
    {
        var tracker = NewTracker();
        var middleware = new ApiActivityMiddleware(_ => Task.CompletedTask, tracker);
        var context = new DefaultHttpContext();
        context.Request.Path = "/health/live";

        await middleware.InvokeAsync(context);

        tracker.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Luon_chuyen_yeu_cau_cho_buoc_ke_tiep()
    {
        var reached = 0;
        var middleware = new ApiActivityMiddleware(
            _ =>
            {
                reached++;
                return Task.CompletedTask;
            },
            NewTracker());

        await middleware.InvokeAsync(AuthenticatedContext());
        await middleware.InvokeAsync(new DefaultHttpContext());

        reached.Should().Be(2);
    }
}
