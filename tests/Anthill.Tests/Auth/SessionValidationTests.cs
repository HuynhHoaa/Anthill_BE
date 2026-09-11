using Anthill.Application.Common;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Auth;

/// <summary>
/// Phép kiểm phiên chạy trên mọi request có token, nên mỗi nhánh sai ở đây là một lối để token cũ
/// của tài khoản bị khoá hay vừa đổi mật khẩu đi tiếp.
/// </summary>
public class SessionValidationTests
{
    private const string Stamp = "6f1c2d9ab34e4f0a8c7d5e6f7a8b9c0d";

    private static AccountSessionState Account(UserStatus status = UserStatus.Active, string stamp = Stamp) =>
        new(42, status, stamp);

    [Fact]
    public void Tai_khoan_hoat_dong_va_stamp_khop_thi_hop_le()
    {
        SessionValidation.Evaluate(Stamp, Account()).Should().Be(SessionVerdict.Valid);
    }

    [Fact]
    public void Tai_khoan_khong_con_thi_tu_choi()
    {
        SessionValidation.Evaluate(Stamp, null).Should().Be(SessionVerdict.AccountMissing);
    }

    /// <summary>Đổi mật khẩu, được cấp lại mật khẩu, hay khoá rồi mở khoá: tài khoản vẫn hoạt động nhưng stamp đã xoay.</summary>
    [Fact]
    public void Stamp_da_xoay_tren_tai_khoan_hoat_dong_thi_tu_choi()
    {
        SessionValidation.Evaluate(Stamp, Account(stamp: "0000000000000000000000000000000a"))
            .Should().Be(SessionVerdict.StampChanged);
    }

    [Fact]
    public void So_stamp_phan_biet_hoa_thuong()
    {
        SessionValidation.Evaluate(Stamp.ToUpperInvariant(), Account()).Should().Be(SessionVerdict.StampChanged);
    }

    /// <summary>
    /// Trước đây một token thiếu claim này được coi là "không có gì để so" và lọt qua. Token do hệ
    /// thống cấp luôn có nó, nên thiếu là dấu hiệu token không phải của mình.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Thieu_stamp_thi_tu_choi_chu_khong_bo_qua(string? stamp)
    {
        SessionValidation.Evaluate(stamp, Account()).Should().Be(SessionVerdict.MalformedToken);
    }

    /// <summary>Stamp khớp không cứu được tài khoản đã bị chặn — kể cả khi ai đó quên xoay stamp.</summary>
    [Theory]
    [InlineData(UserStatus.Locked, SessionVerdict.Locked)]
    [InlineData(UserStatus.PendingDeletion, SessionVerdict.PendingDeletion)]
    [InlineData(UserStatus.Deleted, SessionVerdict.Deleted)]
    public void Tai_khoan_khong_hoat_dong_thi_tu_choi_du_stamp_khop(UserStatus status, SessionVerdict expected)
    {
        SessionValidation.Evaluate(Stamp, Account(status)).Should().Be(expected);
    }

    /// <summary>
    /// Khoá và đưa vào diện xoá đều xoay stamp. Kết luận phải là trạng thái, vì câu "đăng nhập lại"
    /// của nhánh stamp là lời khuyên sai — đăng nhập lại cũng bị chặn.
    /// </summary>
    [Theory]
    [InlineData(UserStatus.Locked, SessionVerdict.Locked)]
    [InlineData(UserStatus.PendingDeletion, SessionVerdict.PendingDeletion)]
    [InlineData(UserStatus.Deleted, SessionVerdict.Deleted)]
    public void Trang_thai_duoc_xet_truoc_stamp(UserStatus status, SessionVerdict expected)
    {
        SessionValidation.Evaluate(Stamp, Account(status, "ffffffffffffffffffffffffffffffff"))
            .Should().Be(expected);
    }

    /// <summary>Một trạng thái thêm vào enum sau này mà chưa ai dạy hàm này: đóng cửa trước.</summary>
    [Fact]
    public void Trang_thai_la_thi_dong_cua()
    {
        SessionValidation.Evaluate(Stamp, Account((UserStatus)99)).Should().Be(SessionVerdict.Locked);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("42", 42)]
    [InlineData("2147483647", int.MaxValue)]
    public void Doc_duoc_sub_hop_le(string subject, int expected)
    {
        SessionValidation.TryReadUserId(subject, out var userId).Should().BeTrue();
        userId.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData(" 42")]
    [InlineData("4.2")]
    [InlineData("99999999999")]
    public void Sub_hong_thi_khong_doc_duoc(string? subject)
    {
        SessionValidation.TryReadUserId(subject, out _).Should().BeFalse();
    }

    /// <summary>
    /// Chỉ ba kết luận do trạng thái tài khoản mới được /api/auth/me tự trả lời bằng 403. Token hỏng
    /// hay stamp đã xoay mà lọt vào nhóm này thì endpoint đó thành lối vào cho token cũ.
    /// </summary>
    [Theory]
    [InlineData(SessionVerdict.Locked, true)]
    [InlineData(SessionVerdict.PendingDeletion, true)]
    [InlineData(SessionVerdict.Deleted, true)]
    [InlineData(SessionVerdict.Valid, false)]
    [InlineData(SessionVerdict.MalformedToken, false)]
    [InlineData(SessionVerdict.AccountMissing, false)]
    [InlineData(SessionVerdict.StampChanged, false)]
    public void Chi_ba_trang_thai_tai_khoan_la_bi_chan_do_tai_khoan(SessionVerdict verdict, bool expected)
    {
        SessionValidation.IsBlockedAccount(verdict).Should().Be(expected);
    }

    [Fact]
    public void Moi_ket_luan_tu_choi_deu_co_cau_chu_rieng_cho_nguoi_dung()
    {
        var rejected = Enum.GetValues<SessionVerdict>().Where(verdict => verdict != SessionVerdict.Valid).ToArray();

        rejected.Should().AllSatisfy(verdict => SessionValidation.MessageFor(verdict).Should().NotBeNullOrWhiteSpace());
        SessionValidation.MessageFor(SessionVerdict.Locked).Should().Contain("khoá");
        SessionValidation.MessageFor(SessionVerdict.PendingDeletion).Should().Contain("chờ xoá");
        SessionValidation.MessageFor(SessionVerdict.StampChanged).Should().Contain("đăng nhập lại");
    }

    /// <summary>Đăng nhập, /api/auth/me và phép kiểm phiên phải nói cùng một câu cho cùng một trạng thái.</summary>
    [Theory]
    [InlineData(UserStatus.Locked, SessionVerdict.Locked)]
    [InlineData(UserStatus.PendingDeletion, SessionVerdict.PendingDeletion)]
    [InlineData(UserStatus.Deleted, SessionVerdict.Deleted)]
    public void Cau_chu_trung_voi_cau_cua_dang_nhap(UserStatus status, SessionVerdict verdict)
    {
        SessionValidation.MessageFor(verdict).Should().Be(SessionValidation.BlockedStatusMessage(status));
    }

    [Fact]
    public void Cau_khoa_tai_khoan_giu_nguyen_chu_cu()
    {
        // Bộ kiểm HTTP step2 đối chiếu nguyên văn câu này ở /api/auth/me.
        SessionValidation.BlockedStatusMessage(UserStatus.Locked)
            .Should().Be("Tài khoản đã bị khoá, liên hệ quản trị viên.");
    }
}
