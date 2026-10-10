using Anthill.Application.Common;
using FluentAssertions;

namespace Anthill.Tests.Attempts;

/// <summary>
/// Hạn giờ tính bằng đồng hồ server (§3 quy tắc ③).
/// </summary>
/// <remarks>
/// Hai chỗ dễ sai và cùng làm mất bài của học sinh: <b>phép kẹp vào giờ đóng đề</b> — thiếu nó thì
/// một em bấm bắt đầu sát giờ đóng vẫn được cấp trọn thời lượng và làm bài sau khi cửa đã đóng cho
/// cả lớp — và <b>giây cuối cùng</b>, nơi một dấu <c>&gt;</c> viết thành <c>&gt;=</c> khiến bài nộp
/// đúng lúc chuông reo bị từ chối.
/// </remarks>
public class AttemptDeadlineTests
{
    private static readonly DateTime StartedAt = new(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CloseAt = new(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Han_gio_binh_thuong_la_luc_bat_dau_cong_thoi_luong()
    {
        AttemptDeadline.Resolve(StartedAt, 45, CloseAt).Should().Be(StartedAt.AddMinutes(45));
    }

    [Fact]
    public void Bat_dau_sat_gio_dong_thi_han_gio_bi_kep_ve_gio_dong()
    {
        // Còn 10 phút nữa đề đóng mà thời lượng là 45 phút: không kẹp thì em này làm bài tới
        // 08:50 trong khi cả lớp đã dừng lúc 08:10.
        var startedAt = CloseAt.AddMinutes(-10);

        AttemptDeadline.Resolve(startedAt, 45, CloseAt).Should().Be(CloseAt);
    }

    [Fact]
    public void Han_gio_roi_dung_gio_dong_thi_khong_bi_cat_them()
    {
        var startedAt = CloseAt.AddMinutes(-45);

        AttemptDeadline.Resolve(startedAt, 45, CloseAt).Should().Be(CloseAt);
    }

    [Fact]
    public void Moc_thoi_gian_khong_ghi_Kind_van_duoc_hieu_la_UTC()
    {
        // EF Core đọc cột timestamptz ra DateTime có Kind = Unspecified ở vài đường đi. Không chốt
        // Kind trước khi so thì kết quả lệch đúng bằng chênh lệch múi giờ của máy chủ — trên máy
        // giờ Việt Nam là bảy tiếng, quá đủ để một bài hết giờ vẫn nhận thêm câu trả lời.
        var unspecifiedClose = DateTime.SpecifyKind(CloseAt, DateTimeKind.Unspecified);
        var unspecifiedStart = DateTime.SpecifyKind(CloseAt.AddMinutes(-10), DateTimeKind.Unspecified);

        AttemptDeadline.Resolve(unspecifiedStart, 45, unspecifiedClose).Should().Be(CloseAt);
        AttemptDeadline.IsExpired(unspecifiedClose, CloseAt.AddSeconds(1)).Should().BeTrue();
    }

    [Fact]
    public void Truoc_han_thi_chua_het_gio()
    {
        AttemptDeadline.IsExpired(CloseAt, CloseAt.AddSeconds(-1)).Should().BeFalse();
    }

    [Fact]
    public void Dung_giay_het_gio_van_con_kip()
    {
        // Học sinh bấm Nộp ở giây cuối cùng thì bài phải được nhận. Cùng quy ước với phép tính vòng
        // đời của đề ở mốc closeAt.
        AttemptDeadline.IsExpired(CloseAt, CloseAt).Should().BeFalse();
    }

    [Fact]
    public void Sau_giay_het_gio_thi_da_muon()
    {
        AttemptDeadline.IsExpired(CloseAt, CloseAt.AddSeconds(1)).Should().BeTrue();
    }

    [Fact]
    public void So_giay_con_lai_lam_tron_len()
    {
        // Còn 0,4 giây mà trả về 0 thì màn hình hiện "hết giờ" trong khi server vẫn còn nhận bài.
        AttemptDeadline.RemainingSeconds(CloseAt, CloseAt.AddMilliseconds(-400)).Should().Be(1);
        AttemptDeadline.RemainingSeconds(CloseAt, CloseAt.AddSeconds(-30)).Should().Be(30);
    }

    [Fact]
    public void So_giay_con_lai_khong_bao_gio_am()
    {
        // Đồng hồ đếm ngược của frontend nhận thẳng con số này; một giá trị âm sẽ hiện thành
        // "-00:05" trên màn hình của học sinh.
        AttemptDeadline.RemainingSeconds(CloseAt, CloseAt.AddMinutes(5)).Should().Be(0);
        AttemptDeadline.RemainingSeconds(CloseAt, CloseAt).Should().Be(0);
    }

    [Fact]
    public void Bai_he_thong_nop_thay_muon_hang_gio_van_ghi_gio_nop_bang_han()
    {
        // Em bỏ thi, tiến trình nền chạm tới lượt này sau hạn ba tiếng: giờ nộp không được kéo
        // "thời gian đã làm" vượt thời lượng của đề.
        var lateSweep = CloseAt.AddHours(3);

        AttemptDeadline.RecordedSubmitTime(CloseAt, lateSweep, autoSubmitted: true).Should().Be(CloseAt);
    }

    [Fact]
    public void Bai_nop_thay_trong_khoang_an_han_cung_ghi_bang_han()
    {
        // Request tự nộp của client chạm server trễ vài trăm mili giây là chuyện bình thường.
        AttemptDeadline.RecordedSubmitTime(CloseAt, CloseAt.AddMilliseconds(400), autoSubmitted: true)
            .Should().Be(CloseAt);
    }

    [Fact]
    public void Bai_he_thong_nop_thay_ma_chua_toi_han_giu_gio_that()
    {
        // Không xảy ra theo cách ghi hiện tại, nhưng nếu đồng hồ hai instance lệch nhau thì không
        // được ghi một giờ nộp ở tương lai.
        var early = CloseAt.AddSeconds(-5);

        AttemptDeadline.RecordedSubmitTime(CloseAt, early, autoSubmitted: true).Should().Be(early);
    }

    [Fact]
    public void Bai_em_tu_nop_truoc_gio_giu_nguyen_gio_that()
    {
        var submitted = CloseAt.AddMinutes(-7);

        AttemptDeadline.RecordedSubmitTime(CloseAt, submitted, autoSubmitted: false).Should().Be(submitted);
    }

    [Fact]
    public void Gio_nop_ghi_so_cung_chiu_moc_khong_ghi_Kind()
    {
        var unspecifiedExpiry = DateTime.SpecifyKind(CloseAt, DateTimeKind.Unspecified);

        AttemptDeadline.RecordedSubmitTime(unspecifiedExpiry, CloseAt.AddHours(1), autoSubmitted: true)
            .Should().Be(CloseAt);
    }
}
