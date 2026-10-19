using Anthill.Application.Services;
using FluentAssertions;
using Xunit;

namespace Anthill.Tests.Imports;

/// <summary>
/// Mã của môn được tạo từ tên ghi trong file Excel: ngắn, không dấu, in hoa và không bao giờ trùng mã đã có (cột Code là duy nhất,
/// không phân biệt hoa thường).
/// </summary>
public class SchoolImportSubjectCodeTests
{
    [Theory]
    [InlineData("Khoa học tự nhiên", "KHTN")]
    [InlineData("Anh văn", "AV")]
    [InlineData("Toán hình", "TH")]
    [InlineData("Toán", "TOAN")]
    [InlineData("Văn", "VAN")]
    [InlineData("Đạo đức", "DD")]
    [InlineData("  Giáo   dục  công dân ", "GDCD")]
    [InlineData("Tin học (nâng cao)", "THNC")]
    public void Ma_mon_la_chu_cai_dau_cac_tu_khong_dau_in_hoa(string name, string expected)
    {
        SchoolImportService.NewSubjectCode(name, new HashSet<string>()).Should().Be(expected);
    }

    [Fact]
    public void Ten_toan_ky_hieu_van_ra_mot_ma_hop_le()
    {
        SchoolImportService.NewSubjectCode("???", new HashSet<string>()).Should().Be("MON");
    }

    [Fact]
    public void Ma_trung_thi_them_so_va_khong_phan_biet_hoa_thuong()
    {
        var taken = new HashSet<string> { "av", "av2" };

        SchoolImportService.NewSubjectCode("Anh văn", taken).Should().Be("AV3");
        taken.Should().Contain("av3");
    }

    [Fact]
    public void Hai_mon_cung_chu_cai_dau_trong_mot_lan_nhap_khong_trung_ma()
    {
        var taken = new HashSet<string>();

        var first = SchoolImportService.NewSubjectCode("Toán hình", taken);
        var second = SchoolImportService.NewSubjectCode("Tiếng Hàn", taken);

        first.Should().Be("TH");
        second.Should().Be("TH2");
    }
}
