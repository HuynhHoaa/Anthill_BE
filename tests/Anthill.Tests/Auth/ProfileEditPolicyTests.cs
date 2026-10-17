using Anthill.Application.Common;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Auth;

public class ProfileEditPolicyTests
{
    private const string Name = "Nguyễn Văn An";
    private const string Email = "an.nv@truong.edu.vn";
    private const string Phone = "0912345678";

    private static IReadOnlyList<string> Check(
        UserRole role, string? fullName = null, string? email = null, string? phone = Phone) =>
        ProfileEditPolicy.LockedFieldsChanged(
            role, Name, Email, Phone, fullName ?? Name, email ?? Email, phone);

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Student)]
    public void Gui_lai_dung_gia_tri_dang_co_thi_khong_bi_chan(UserRole role)
    {
        Check(role).Should().BeEmpty();
    }

    [Fact]
    public void Quan_tri_vien_sua_duoc_ca_ba_truong()
    {
        Check(UserRole.Admin, fullName: "Tên khác", email: "khac@truong.edu.vn", phone: "0987654321")
            .Should().BeEmpty();
    }

    [Fact]
    public void Giao_vien_doi_email_hoac_so_dien_thoai_bi_chan()
    {
        Check(UserRole.Teacher, email: "khac@truong.edu.vn")
            .Should().Equal(ProfileEditPolicy.EmailField);

        Check(UserRole.Teacher, phone: "0987654321")
            .Should().Equal(ProfileEditPolicy.PhoneField);

        Check(UserRole.Teacher, email: "khac@truong.edu.vn", phone: null)
            .Should().Equal(ProfileEditPolicy.EmailField, ProfileEditPolicy.PhoneField);
    }

    [Fact]
    public void Giao_vien_van_doi_duoc_ho_ten()
    {
        Check(UserRole.Teacher, fullName: "Nguyễn Văn Bình").Should().BeEmpty();
    }

    [Fact]
    public void Hoc_sinh_khong_doi_duoc_ho_ten_email_va_so_dien_thoai()
    {
        Check(UserRole.Student, fullName: "Tên khác", email: "khac@truong.edu.vn", phone: "0987654321")
            .Should().Equal(
                ProfileEditPolicy.FullNameField, ProfileEditPolicy.EmailField, ProfileEditPolicy.PhoneField);
    }

    [Fact]
    public void Email_so_sanh_khong_phan_biet_hoa_thuong()
    {
        // Form gửi lại đúng email đang có; chênh chữ hoa không được tính là đổi.
        Check(UserRole.Teacher, email: "AN.NV@truong.edu.vn").Should().BeEmpty();
    }

    [Fact]
    public void So_dien_thoai_rong_va_null_la_mot_nghia()
    {
        var withoutPhone = ProfileEditPolicy.LockedFieldsChanged(
            UserRole.Student, Name, Email, currentPhone: null, Name, Email, phone: null);
        withoutPhone.Should().BeEmpty();

        var blankInDatabase = ProfileEditPolicy.LockedFieldsChanged(
            UserRole.Student, Name, Email, currentPhone: "  ", Name, Email, phone: null);
        blankInDatabase.Should().BeEmpty();
    }

    [Fact]
    public void Dien_so_khi_chua_co_so_cung_bi_chan()
    {
        // Học sinh chưa có số mà gõ vào ô số điện thoại: đó là một thay đổi.
        var changed = ProfileEditPolicy.LockedFieldsChanged(
            UserRole.Student, Name, Email, currentPhone: null, Name, Email, phone: Phone);

        changed.Should().Equal(ProfileEditPolicy.PhoneField);
    }
}
