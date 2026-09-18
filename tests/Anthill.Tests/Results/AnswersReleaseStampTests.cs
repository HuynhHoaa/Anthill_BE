using Anthill.Application.Common;
using FluentAssertions;

namespace Anthill.Tests.Results;

/// <summary>
/// H10 (nửa release-flicker) — một khi mốc mở đáp án được ghi nhận là đã qua, nó phải chốt cứng và
/// không được tính lại theo <c>MAX(CloseAt)</c> động nữa. Trước bản vá này, gán thêm một lượt giao mới
/// (một suất thi bù cho lớp khác, chẳng hạn) với giờ đóng muộn hơn kéo MAX động ra xa, khiến những em
/// đã hợp lệ xem đáp án bị giấu lại đáp án giữa chừng — pin lại từng góc của bảng quyết định ở đây.
/// </summary>
public class AnswersReleaseStampTests
{
    private static readonly DateTime Now = new(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Da_chot_tu_truoc_thi_dung_lai_khong_tinh_dong_nua()
    {
        // Mốc động bây giờ có thể là bất cứ gì (kể cả NULL, hay ở tương lai vì một lượt giao mới vừa
        // kéo MAX ra xa) — một khi đã chốt thì không quan tâm nữa.
        var resolution = AnswersReleaseStamp.Resolve(
            storedReleasedAt: Now.AddDays(-3),
            dynamicReleaseAt: Now.AddDays(30),
            asOfUtc: Now);

        resolution.EffectiveReleaseAt.Should().Be(Now.AddDays(-3));
        resolution.StampToPersist.Should().BeNull("đã chốt rồi thì không ghi lại nữa");
    }

    [Fact]
    public void Lan_dau_moc_dong_da_qua_thi_chot_lai_va_dung_luon_gia_tri_do()
    {
        var dynamicReleaseAt = Now.AddHours(-1);

        var resolution = AnswersReleaseStamp.Resolve(
            storedReleasedAt: null,
            dynamicReleaseAt: dynamicReleaseAt,
            asOfUtc: Now);

        resolution.EffectiveReleaseAt.Should().Be(dynamicReleaseAt);
        resolution.StampToPersist.Should().Be(dynamicReleaseAt, "lần đầu ghi nhận đã qua giờ thì phải chốt ngay");
    }

    [Fact]
    public void Dung_bang_moc_dong_cung_duoc_coi_la_da_qua_va_chot_lai()
    {
        var resolution = AnswersReleaseStamp.Resolve(storedReleasedAt: null, dynamicReleaseAt: Now, asOfUtc: Now);

        resolution.StampToPersist.Should().Be(Now);
    }

    [Fact]
    public void Moc_dong_con_o_tuong_lai_thi_khong_chot_gi_ca()
    {
        var dynamicReleaseAt = Now.AddHours(1);

        var resolution = AnswersReleaseStamp.Resolve(
            storedReleasedAt: null,
            dynamicReleaseAt: dynamicReleaseAt,
            asOfUtc: Now);

        // Vẫn trả mốc động để màn hình hẹn được "mở lúc mấy giờ", nhưng chưa có gì để ghi xuống.
        resolution.EffectiveReleaseAt.Should().Be(dynamicReleaseAt);
        resolution.StampToPersist.Should().BeNull();
    }

    [Fact]
    public void Chua_tung_giao_cho_ai_thi_khong_chot_gi_ca()
    {
        var resolution = AnswersReleaseStamp.Resolve(storedReleasedAt: null, dynamicReleaseAt: null, asOfUtc: Now);

        resolution.EffectiveReleaseAt.Should().BeNull();
        resolution.StampToPersist.Should().BeNull();
    }

    [Fact]
    public void Kich_ban_hoi_quy_gan_them_luot_giao_moi_khong_giau_lai_dap_an_da_chot()
    {
        // Ca đúng lỗi H10 nửa release-flicker: lượt giao đầu đã đóng (đáp án lộ ra, được chốt lại ở lần
        // đọc trước). Một lượt giao MỚI với giờ đóng muộn hơn xuất hiện — nếu còn tính động thì MAX mới
        // sẽ đẩy "mốc mở đáp án" ra xa, và em đã hợp lệ xem đáp án sẽ bị giấu lại. Có cột đã chốt thì
        // không còn phụ thuộc lượt giao mới đó nữa.
        var chotTuLanDocTruoc = Now.AddDays(-1);
        var maxDongSauKhiThemLuotGiaoMoi = Now.AddDays(10); // sẽ là dynamicReleaseAt nếu còn tính động

        var resolution = AnswersReleaseStamp.Resolve(
            storedReleasedAt: chotTuLanDocTruoc,
            dynamicReleaseAt: maxDongSauKhiThemLuotGiaoMoi,
            asOfUtc: Now);

        resolution.EffectiveReleaseAt.Should().Be(chotTuLanDocTruoc, "không được lùi lại vì một lượt giao mới");
    }
}
