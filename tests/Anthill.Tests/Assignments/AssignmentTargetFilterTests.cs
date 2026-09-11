using Anthill.Application.Common;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Assignments;

/// <summary>
/// Em da roi truong khong duoc bao la "dang khoa": hai ly do dan toi hai hanh dong khac nhau cua
/// giao vien. Va moi em chi duoc dem o dung mot ly do.
/// </summary>
public class AssignmentTargetFilterTests
{
    private const int MyGroup = 5;
    private static readonly IReadOnlySet<int> MyGroups = new HashSet<int> { MyGroup };

    private static User Student(UserStatus status, int? groupId = MyGroup) => new()
    {
        Id = 3,
        FullName = "Tran Thi Lan",
        Email = "lan.tt@anthill.edu.vn",
        Username = "lan.tt",
        PasswordHash = "hash",
        SecurityStamp = "stamp",
        AvatarColor = "#2563eb",
        Role = UserRole.Student,
        Status = status,
        GroupId = groupId
    };

    [Theory]
    [InlineData(UserStatus.Active, AssignmentSkipReason.None)]
    [InlineData(UserStatus.Locked, AssignmentSkipReason.Locked)]
    [InlineData(UserStatus.PendingDeletion, AssignmentSkipReason.Retired)]
    public void Em_trong_lop_cua_minh_duoc_phan_loai_theo_trang_thai(UserStatus status, AssignmentSkipReason expected)
    {
        AssignmentTargetFilter.Classify(Student(status), MyGroups).Should().Be(expected);
    }

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.PendingDeletion)]
    public void Em_lop_khac_chi_tinh_la_ngoai_pham_vi_du_trang_thai_nao(UserStatus status)
    {
        // Pham vi dung truoc: trang thai tai khoan cua em lop khac khong lot ra qua con so dem.
        AssignmentTargetFilter.Classify(Student(status, groupId: 99), MyGroups)
            .Should().Be(AssignmentSkipReason.OutOfScope);
    }

    [Fact]
    public void Em_chua_phan_lop_la_ngoai_pham_vi()
    {
        // Tai khoan da xoa bi go khoi lop (GroupId = NULL) nen roi vao nhanh nay truoc. Day la duong duy
        // nhat mot tai khoan da xoa di qua bo loc, nen skippedRetired chi dem em dang cho xoa.
        AssignmentTargetFilter.Classify(Student(UserStatus.Deleted, groupId: null), MyGroups)
            .Should().Be(AssignmentSkipReason.OutOfScope);
    }
}
