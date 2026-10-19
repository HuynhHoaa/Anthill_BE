using Anthill.Application.DTOs.Terms;
using Anthill.Application.Validators;
using FluentAssertions;

namespace Anthill.Tests.Validators;

/// <summary>Kỳ học: tên bắt buộc và không dài quá, ngày kết thúc không trước ngày bắt đầu.</summary>
public class SaveAcademicTermRequestValidatorTests
{
    private readonly SaveAcademicTermRequestValidator _validator = new();

    [Fact]
    public void Ten_ky_hop_le_chi_can_ten()
    {
        _validator.Validate(new SaveAcademicTermRequest { Name = "Học kỳ 1 · 2026–2027" }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ten_ky_bat_buoc(string name)
    {
        var result = _validator.Validate(new SaveAcademicTermRequest { Name = name });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(SaveAcademicTermRequest.Name));
    }

    [Fact]
    public void Ten_ky_toi_da_100_ky_tu_sau_khi_cat_khoang_trang()
    {
        _validator.Validate(new SaveAcademicTermRequest { Name = "  " + new string('a', 100) + "  " }).IsValid.Should().BeTrue();
        _validator.Validate(new SaveAcademicTermRequest { Name = new string('a', 101) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Ngay_ket_thuc_khong_truoc_ngay_bat_dau()
    {
        var start = new DateOnly(2026, 9, 1);

        _validator.Validate(new SaveAcademicTermRequest { Name = "Kỳ 1", StartsOn = start, EndsOn = start }).IsValid.Should().BeTrue();
        _validator.Validate(new SaveAcademicTermRequest { Name = "Kỳ 1", StartsOn = start, EndsOn = start.AddDays(-1) }).IsValid.Should().BeFalse();
        _validator.Validate(new SaveAcademicTermRequest { Name = "Kỳ 1", EndsOn = start }).IsValid.Should().BeTrue("chỉ có một mốc thì không có gì để so");
    }
}
