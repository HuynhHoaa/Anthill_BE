using Anthill.Application.Common;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Auth;

public class AvatarPolicyTests
{
    [Theory]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.Teacher, true)]
    [InlineData(UserRole.Student, false)]
    public void Chi_quan_tri_vien_va_giao_vien_doi_duoc_anh(UserRole role, bool expected)
    {
        AvatarPolicy.CanChange(role).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Chuoi_trong_la_lenh_go_anh(string? input)
    {
        var normalized = AvatarPolicy.Normalize(input);

        normalized.Should().BeNull();
        AvatarPolicy.Validate(normalized).Should().BeNull();
    }

    [Fact]
    public void Cat_khoang_trang_hai_dau()
    {
        AvatarPolicy.Normalize("  /uploads/images/a.png  ").Should().Be("/uploads/images/a.png");
    }

    [Fact]
    public void Duong_dan_do_upload_cap_la_hop_le()
    {
        AvatarPolicy.Validate("/uploads/images/3f2a9c.png").Should().BeNull();
    }

    [Theory]
    [InlineData("https://nguoi-la.example/anh.png")]
    [InlineData("//nguoi-la.example/uploads/anh.png")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("uploads/images/a.png")]
    [InlineData("/avatars/a.png")]
    [InlineData("/uploads/../secrets/a.png")]
    public void Duong_dan_khong_phai_anh_da_tai_len_bi_tu_choi(string url)
    {
        AvatarPolicy.Validate(url).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Duong_dan_qua_dai_bi_tu_choi()
    {
        var url = "/uploads/images/" + new string('a', AvatarPolicy.MaxUrlLength);

        AvatarPolicy.Validate(url).Should().Contain(AvatarPolicy.MaxUrlLength.ToString());
    }

    [Fact]
    public void Duong_dan_dung_do_dai_toi_da_van_hop_le()
    {
        var prefix = "/uploads/images/";
        var url = prefix + new string('a', AvatarPolicy.MaxUrlLength - prefix.Length);

        url.Length.Should().Be(AvatarPolicy.MaxUrlLength);
        AvatarPolicy.Validate(url).Should().BeNull();
    }
}
