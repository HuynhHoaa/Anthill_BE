using Anthill.Application.Common;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Attempts;

/// <summary>
/// Lưu nháp hay nạp lại phiên sau giờ hết phải ra cùng một mã, bất kể ai chốt sổ lượt đó trước.
/// </summary>
/// <remarks>
/// Lỗi cũ chỉ lộ ra khi tiến trình tự nộp bài chạy đúng vào vài giây giữa giờ hết và gói nháp kế
/// tiếp — nghĩa là một nửa số lần chạy <c>step6-attempts.sh</c>. Ở đây từng trạng thái được kiểm
/// thẳng, không cần chờ tới lượt quét của tiến trình nền.
/// </remarks>
public class AttemptClosureTests
{
    private static readonly DateTime Now = new(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Tien_trinh_nen_chot_truoc_thi_van_la_het_gio()
    {
        // Đúng cuộc đua gây ra phép kiểm chập chờn: lượt đã Graded với cờ tự nộp khi gói nháp tới.
        AttemptClosure.ClosedByDeadline(AssignmentStatus.Graded, autoSubmitted: true, Now.AddSeconds(-5), Now)
            .Should().BeTrue();
    }

    [Fact]
    public void Dang_lam_do_ma_qua_han_thi_la_het_gio()
    {
        AttemptClosure.ClosedByDeadline(AssignmentStatus.InProgress, autoSubmitted: false, Now.AddSeconds(-1), Now)
            .Should().BeTrue();
    }

    [Fact]
    public void Dang_lam_do_con_han_thi_chua_dong()
    {
        AttemptClosure.ClosedByDeadline(AssignmentStatus.InProgress, autoSubmitted: false, Now.AddMinutes(10), Now)
            .Should().BeFalse();
    }

    [Fact]
    public void Dung_giay_het_gio_van_chua_dong()
    {
        // Cùng quy ước so chặt với AttemptDeadline.IsExpired.
        AttemptClosure.ClosedByDeadline(AssignmentStatus.InProgress, autoSubmitted: false, Now, Now)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(AssignmentStatus.Submitted)]
    [InlineData(AssignmentStatus.Graded)]
    public void Em_tu_nop_truoc_gio_thi_khong_phai_het_gio_ke_ca_khi_gio_da_qua(AssignmentStatus status)
    {
        // Em nộp lúc còn mười phút rồi tab cũ gửi thêm một gói nháp sau giờ hết: câu đúng là
        // "em đã nộp rồi" (409), không phải "hết giờ, hệ thống đã nộp hộ" (410).
        AttemptClosure.ClosedByDeadline(status, autoSubmitted: false, Now.AddMinutes(-5), Now)
            .Should().BeFalse();
    }

    [Fact]
    public void Tu_nop_nhung_dong_ho_request_chua_qua_han_thi_khong_khang_dinh_het_gio()
    {
        // Hai instance API lệch đồng hồ vài mili giây: không nói "hết giờ" khi chính request này
        // còn thấy lượt trong hạn.
        AttemptClosure.ClosedByDeadline(AssignmentStatus.Graded, autoSubmitted: true, Now.AddMilliseconds(3), Now)
            .Should().BeFalse();
    }

    [Fact]
    public void Luot_bi_danh_dau_het_han_thi_la_het_gio()
    {
        AttemptClosure.ClosedByDeadline(AssignmentStatus.Expired, autoSubmitted: false, Now.AddMinutes(-5), Now)
            .Should().BeTrue();
    }

    [Fact]
    public void Chua_bat_dau_thi_khong_co_gi_de_dong()
    {
        AttemptClosure.ClosedByDeadline(AssignmentStatus.Assigned, autoSubmitted: false, Now.AddMinutes(-5), Now)
            .Should().BeFalse();
    }
}
