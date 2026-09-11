using Anthill.Application.Mappings;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Students;

/// <summary>
/// Hai bộ phân tích trạng thái tách nhau có chủ đích: một cho việc ĐẶT, một cho việc LỌC.
/// </summary>
/// <remarks>
/// Trước đây chỉ có một, nhận cả bốn trạng thái, nên PATCH status đặt thẳng "deleted" được — tài
/// khoản mang nhãn đã xoá trong khi họ tên, email vẫn còn nguyên vì bước ẩn danh hoá không chạy.
/// Nhưng ô lọc lại CẦN nhận cả bốn để quản trị viên tìm ra tài khoản chờ xoá. Các test dưới đây
/// ghim đúng ranh giới đó, để lần sau không ai gộp hai hàm lại cho gọn.
/// </remarks>
public class StudentStatusParsingTests
{
    [Theory]
    [InlineData("active", UserStatus.Active)]
    [InlineData("locked", UserStatus.Locked)]
    [InlineData("  ACTIVE  ", UserStatus.Active)]
    [InlineData("Locked", UserStatus.Locked)]
    public void Dat_truc_tiep_chi_nhan_active_va_locked(string value, UserStatus expected)
    {
        StudentMappingExtensions.TryParseSettableStatus(value, out var status).Should().BeTrue();
        status.Should().Be(expected);
    }

    /// <summary>
    /// Hai trạng thái của quy trình xoá chỉ đi qua request-deletion và restore, vì mỗi lối phải làm
    /// thêm việc — ghi lý do, xoay SecurityStamp, ẩn danh hoá, xoá dấu vết khi khôi phục.
    /// </summary>
    [Theory]
    [InlineData("deleted")]
    [InlineData("pendingDeletion")]
    [InlineData("PendingDeletion")]
    [InlineData("DELETED")]
    public void Dat_truc_tiep_tu_choi_hai_trang_thai_cua_quy_trinh_xoa(string value)
    {
        StudentMappingExtensions.TryParseSettableStatus(value, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("xyz")]
    [InlineData("42")]
    [InlineData("all")]
    public void Dat_truc_tiep_tu_choi_gia_tri_la_va_rong(string? value)
    {
        StudentMappingExtensions.TryParseSettableStatus(value, out _).Should().BeFalse();
    }

    /// <summary>
    /// Enum.TryParse nhận chuỗi số và trả về giá trị enum không tồn tại; chặn bằng Enum.IsDefined.
    /// Nếu không, "?status=42" thành một bộ lọc rỗng trông như thật.
    /// </summary>
    [Fact]
    public void Tu_choi_chuoi_so_khong_ung_voi_trang_thai_nao()
    {
        StudentMappingExtensions.TryParseSettableStatus("99", out _).Should().BeFalse();
        StudentMappingExtensions.TryParseStatusFilter("99", out _).Should().BeFalse();
    }

    /// <summary>Ô lọc phải tìm ra được cả tài khoản chờ xoá và đã xoá.</summary>
    [Theory]
    [InlineData("active", UserStatus.Active)]
    [InlineData("locked", UserStatus.Locked)]
    [InlineData("pendingDeletion", UserStatus.PendingDeletion)]
    [InlineData("deleted", UserStatus.Deleted)]
    public void O_loc_nhan_ca_bon_trang_thai(string value, UserStatus expected)
    {
        StudentMappingExtensions.TryParseStatusFilter(value, out var status).Should().BeTrue();
        status.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("all")]
    [InlineData("ALL")]
    public void O_loc_bo_trong_hoac_all_nghia_la_khong_loc(string? value)
    {
        StudentMappingExtensions.TryParseStatusFilter(value, out var status).Should().BeTrue();
        status.Should().BeNull();
    }

    [Fact]
    public void O_loc_tu_choi_gia_tri_la_thay_vi_am_tham_bo_loc()
    {
        StudentMappingExtensions.TryParseStatusFilter("xyz", out _).Should().BeFalse();
    }
}
