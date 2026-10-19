using Anthill.Application.Common;
using FluentAssertions;

namespace Anthill.Tests.Imports;

/// <summary>
/// Chuan hoa chuoi doc tu Excel: cung mot phep o moi noi nhap lieu, de "Lop 10A1" go hai kieu hay ten co hai dau cach deu ra
/// cung mot gia tri. So khop ten lop va nhan ra cung mot hoc sinh deu dua tren <see cref="ImportText.Fold"/>.
/// </summary>
public class ImportTextTests
{
    [Theory]
    [InlineData("  Nguyen   Van  An  ", "Nguyen Van An")]
    [InlineData("Nguyen\tVan An", "Nguyen Van An")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Collapse_gop_khoang_trang_va_cat_hai_dau(string? input, string expected)
    {
        ImportText.Collapse(input).Should().Be(expected);
    }

    [Fact]
    public void Collapse_dua_chuoi_to_hop_NFD_ve_NFC()
    {
        // "ê" gõ kiểu tổ hợp: chữ e cộng dấu mũ rời. Excel trên vài bộ gõ lưu như vậy.
        var decomposed = "Nguyễn";

        var result = ImportText.Collapse(decomposed);

        result.Should().Be("Nguyễn");
        result.Should().NotBe(decomposed);
    }

    [Theory]
    [InlineData("Nguyễn Văn An", "nguyen van an")]
    [InlineData("NGUYỄN VĂN AN", "nguyen van an")]
    [InlineData("  nguyen   van an ", "nguyen van an")]
    [InlineData("Đặng Văn Đức", "dang van duc")]
    [InlineData("Lớp 10A1", "lop 10a1")]
    public void Fold_bo_dau_khong_phan_biet_hoa_thuong_va_khoang_trang(string input, string expected)
    {
        ImportText.Fold(input).Should().Be(expected);
    }

    [Fact]
    public void Fold_coi_hai_cach_go_cung_mot_chu_la_mot()
    {
        ImportText.Fold("Nguyễn").Should().Be(ImportText.Fold("Nguyễn")).And.Be("nguyen");
    }

    [Fact]
    public void Fold_khong_gop_hai_ten_khac_nhau()
    {
        ImportText.Fold("Lop 10A1").Should().NotBe(ImportText.Fold("Lop 10A2"));
        ImportText.Fold("An").Should().NotBe(ImportText.Fold("Anh"));
    }

    [Theory]
    [InlineData("912345678", "0912345678")]
    [InlineData("0912345678", "0912345678")]
    [InlineData("  912345678  ", "0912345678")]
    [InlineData("91234567", "91234567")]
    [InlineData("12345678a", "12345678a")]
    [InlineData("+84912345678", "+84912345678")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Phone_them_lai_so_0_dau_ma_o_kieu_so_cua_Excel_da_nuot(string? input, string expected)
    {
        ImportText.Phone(input).Should().Be(expected);
    }
}
