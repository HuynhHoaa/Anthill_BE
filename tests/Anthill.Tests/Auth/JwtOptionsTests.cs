using Anthill.Application.Validators;
using Anthill.Infrastructure.Authentication;
using FluentAssertions;

namespace Anthill.Tests.Auth;

public class JwtOptionsTests
{
    private static JwtOptions Build(int expiryMinutes) => new()
    {
        SecretKey = new string('k', JwtOptions.MinimumSecretKeyBytes),
        ExpiryMinutes = expiryMinutes
    };

    [Fact]
    public void Mac_dinh_dai_hon_bai_kiem_tra_dai_nhat_duoc_phep()
    {
        new JwtOptions().ExpiryMinutes.Should().BeGreaterThan(SaveExamRequestValidator.MaxDurationMinutes);
    }

    [Fact]
    public void Token_ngan_hon_bai_dai_nhat_cong_phan_bu_bi_tu_choi_luc_khoi_dong()
    {
        var act = () => Build(120).Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*330*");
    }

    [Fact]
    public void Dung_moc_thap_nhat_thi_chap_nhan()
    {
        var act = () => Build(JwtOptions.MinimumExpiryMinutes).Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Mac_dinh_qua_duoc_kiem_tra()
    {
        var act = () => Build(new JwtOptions().ExpiryMinutes).Validate();

        act.Should().NotThrow();
    }
}
