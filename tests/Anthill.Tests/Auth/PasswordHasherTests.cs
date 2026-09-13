using System.Globalization;
using Anthill.Application.Interfaces.Security;
using Anthill.Infrastructure.Authentication;
using FluentAssertions;

namespace Anthill.Tests.Auth;

/// <summary>
/// Chuoi bam gia cua man dang nhap phai ton dung bang thoi gian mot lan doi chieu that. Lech mot bac
/// work factor la lech gap doi thoi gian, va do la du de dem thoi gian tra loi ma doan ra ten dang
/// nhap nao co that.
/// </summary>
public class PasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new PasswordHasher();

    /// <summary>Doc work factor tu tien to cua chuoi bam BCrypt: <c>$2a$12$...</c>.</summary>
    private static int WorkFactorOf(string hash) =>
        int.Parse(hash.Split('$')[2], CultureInfo.InvariantCulture);

    [Fact]
    public void Chuoi_bam_gia_dung_work_factor_cua_mat_khau_that()
    {
        var real = _hasher.Hash("mat-khau-that-cua-mot-tai-khoan");

        WorkFactorOf(_hasher.TimingDecoyHash).Should().Be(WorkFactorOf(real));
    }

    [Fact]
    public void Work_factor_khong_thap_hon_muc_toi_thieu_cua_hop_dong()
    {
        // §5: BCrypt voi work factor >= 11.
        WorkFactorOf(_hasher.Hash("mat-khau-bat-ky")).Should().BeGreaterThanOrEqualTo(11);
    }

    [Fact]
    public void Chuoi_bam_gia_chi_sinh_mot_lan_cho_ca_tien_trinh()
    {
        // Sinh lai o moi lan goi thi moi lan dang nhap sai ten phai tra them mot lan bam day du.
        var first = _hasher.TimingDecoyHash;

        first.Should().Be(_hasher.TimingDecoyHash);
        first.Should().Be(new PasswordHasher().TimingDecoyHash);
    }

    [Fact]
    public void Khong_mat_khau_nao_khop_chuoi_bam_gia()
    {
        _hasher.Verify("mat-khau-bat-ky", _hasher.TimingDecoyHash).Should().BeFalse();
        _hasher.Verify(string.Empty, _hasher.TimingDecoyHash).Should().BeFalse();
    }

    [Fact]
    public void Mat_khau_that_van_doi_chieu_duoc()
    {
        var hash = _hasher.Hash("anthill123");

        _hasher.Verify("anthill123", hash).Should().BeTrue();
        _hasher.Verify("anthill124", hash).Should().BeFalse();
    }
}
