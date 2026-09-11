using Anthill.Application.Common;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Students;

public class StudentAnonymizerTests
{
    private static User BuildStudent(int id = 42) => new()
    {
        Id = id,
        FullName = "Nguyễn Văn Bê",
        Email = "be.nv@anthill.edu.vn",
        Username = "be.nv",
        Code = "HS1234",
        Phone = "0900000000",
        School = "THPT Kiểm Thử",
        AvatarColor = "#6366F1",
        PasswordHash = "hash-cu",
        SecurityStamp = "stamp-cu",
        GroupId = 7,
        Role = UserRole.Student,
        Status = UserStatus.PendingDeletion,
        DeletionReason = "Học sinh chuyển trường từ đầu học kỳ hai.",
        DeletionRequestedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void Anonymize_xoa_moi_du_lieu_ca_nhan()
    {
        var user = BuildStudent();

        StudentAnonymizer.Anonymize(user, DateTime.UtcNow);

        user.FullName.Should().Be(StudentAnonymizer.DisplayName);
        user.Phone.Should().BeNull();
        user.School.Should().BeNull();
        user.Email.Should().NotContain("be.nv");
        user.Username.Should().NotContain("be.nv");
        user.Code.Should().NotBe("HS1234");
    }

    /// <summary>
    /// Ba cột này có chỉ mục duy nhất nên không được để trống hàng loạt — dòng thứ hai sẽ đụng
    /// dòng thứ nhất và cả lượt xoá đổ.
    /// </summary>
    [Fact]
    public void Anonymize_giu_ba_dinh_danh_duy_nhat_khac_nhau_giua_cac_tai_khoan()
    {
        var first = BuildStudent(1);
        var second = BuildStudent(2);

        StudentAnonymizer.Anonymize(first, DateTime.UtcNow);
        StudentAnonymizer.Anonymize(second, DateTime.UtcNow);

        first.Email.Should().NotBe(second.Email);
        first.Username.Should().NotBe(second.Username);
        first.Code.Should().NotBe(second.Code);

        first.Email.Should().NotBeNullOrWhiteSpace();
        first.Username.Should().NotBeNullOrWhiteSpace();
        first.Code.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Lý do xoá phải sống lâu hơn tài khoản: sau khi tên đã biến mất, đây là thứ duy nhất còn
    /// giải thích được vì sao lớp hụt một em.
    /// </summary>
    [Fact]
    public void Anonymize_giu_lai_ly_do_va_moc_yeu_cau()
    {
        var user = BuildStudent();
        var reason = user.DeletionReason;
        var requestedAt = user.DeletionRequestedAt;

        StudentAnonymizer.Anonymize(user, DateTime.UtcNow);

        user.DeletionReason.Should().Be(reason);
        user.DeletionRequestedAt.Should().Be(requestedAt);
        user.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Anonymize_xoay_SecurityStamp_va_doi_mat_khau()
    {
        var user = BuildStudent();

        StudentAnonymizer.Anonymize(user, DateTime.UtcNow);

        user.SecurityStamp.Should().NotBe("stamp-cu");
        user.PasswordHash.Should().NotBe("hash-cu");
        user.Status.Should().Be(UserStatus.Deleted);
    }

    /// <summary>Sĩ số lớp phải phản ánh số em đang thật sự học ở đó.</summary>
    [Fact]
    public void Anonymize_go_hoc_sinh_khoi_lop()
    {
        var user = BuildStudent();

        StudentAnonymizer.Anonymize(user, DateTime.UtcNow);

        user.GroupId.Should().BeNull();
    }

    [Fact]
    public void Anonymize_chay_hai_lan_cho_cung_ket_qua()
    {
        var user = BuildStudent();

        StudentAnonymizer.Anonymize(user, DateTime.UtcNow);
        var afterFirst = (user.Email, user.Username, user.Code);

        StudentAnonymizer.Anonymize(user, DateTime.UtcNow);

        (user.Email, user.Username, user.Code).Should().Be(afterFirst);
    }

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(61, true)]
    public void IsDue_dem_dung_moc_sau_muoi_ngay(int daysElapsed, bool expected)
    {
        var user = BuildStudent();
        var now = user.DeletionRequestedAt!.Value.AddDays(daysElapsed);

        StudentAnonymizer.IsDue(user, now).Should().Be(expected);
    }

    [Fact]
    public void IsDue_bo_qua_tai_khoan_khong_nam_trong_dien_xoa()
    {
        var active = BuildStudent();
        active.Status = UserStatus.Active;

        StudentAnonymizer.IsDue(active, DateTime.UtcNow.AddYears(1)).Should().BeFalse();
    }

    /// <summary>Đã xoá rồi thì không được quét lại — nếu không mỗi lượt quét lại ghi đè DeletedAt.</summary>
    [Fact]
    public void IsDue_bo_qua_tai_khoan_da_xoa()
    {
        var deleted = BuildStudent();
        deleted.Status = UserStatus.Deleted;

        StudentAnonymizer.IsDue(deleted, DateTime.UtcNow.AddYears(1)).Should().BeFalse();
    }
}
