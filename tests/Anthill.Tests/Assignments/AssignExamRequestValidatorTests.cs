using Anthill.Application.DTOs.ExamAssignments;
using Anthill.Application.Validators;
using FluentAssertions;

namespace Anthill.Tests.Assignments;

/// <summary>Hình dạng request giao nhiều lớp một lần, mỗi lớp một khung giờ.</summary>
public class AssignExamRequestValidatorTests
{
    private static readonly DateTime Open = new(2026, 9, 15, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Close = new(2026, 9, 15, 2, 30, 0, DateTimeKind.Utc);

    private readonly AssignExamRequestValidator _validator = new();

    private IReadOnlyList<string> ErrorsOf(AssignExamRequest request) =>
        _validator.Validate(request).Errors.Select(failure => failure.ErrorMessage).ToList();

    [Fact]
    public void Chi_gui_danh_sach_lop_kem_gio_la_hop_le()
    {
        var request = new AssignExamRequest
        {
            Classes =
            [
                new AssignClassTarget { GroupId = 7, OpenAt = Open, CloseAt = Close },
                new AssignClassTarget { GroupId = 8 }
            ]
        };

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Mot_lop_nam_o_ca_hai_danh_sach_bi_tu_choi()
    {
        var request = new AssignExamRequest
        {
            GroupIds = [7],
            Classes = [new AssignClassTarget { GroupId = 7, OpenAt = Open, CloseAt = Close }]
        };

        ErrorsOf(request).Should().Contain(message => message.Contains("chỉ được chọn một lần"));
    }

    [Fact]
    public void Mot_lop_hai_lan_voi_hai_khung_gio_bi_tu_choi()
    {
        var request = new AssignExamRequest
        {
            Classes =
            [
                new AssignClassTarget { GroupId = 7, OpenAt = Open, CloseAt = Close },
                new AssignClassTarget { GroupId = 7 }
            ]
        };

        ErrorsOf(request).Should().Contain(message => message.Contains("chỉ được chọn một lần"));
    }

    [Fact]
    public void Nua_cap_gio_cua_lop_bi_tu_choi()
    {
        var request = new AssignExamRequest
        {
            Classes = [new AssignClassTarget { GroupId = 7, OpenAt = Open }]
        };

        ErrorsOf(request).Should().Contain(message => message.Contains("đủ cả giờ mở và giờ đóng"));
    }

    [Fact]
    public void Gio_dong_cua_lop_truoc_gio_mo_bi_tu_choi()
    {
        var request = new AssignExamRequest
        {
            Classes = [new AssignClassTarget { GroupId = 7, OpenAt = Close, CloseAt = Open }]
        };

        ErrorsOf(request).Should().Contain(message => message.Contains("phải sau giờ mở bài"));
    }

    [Fact]
    public void Muc_lop_rong_bi_tu_choi_thay_vi_thanh_loi_500()
    {
        var request = new AssignExamRequest { Classes = [null!] };

        ErrorsOf(request).Should().Contain(message => message.Contains("mục rỗng"));
    }

    [Fact]
    public void Tran_so_lop_tinh_tren_ca_hai_danh_sach()
    {
        var request = new AssignExamRequest
        {
            GroupIds = Enumerable.Range(1, 30).ToList(),
            Classes = Enumerable.Range(31, 21).Select(id => new AssignClassTarget { GroupId = id }).ToList()
        };

        ErrorsOf(request).Should().Contain(message => message.Contains("tối đa 50 nhóm lớp"));
    }

    [Fact]
    public void Khong_chon_gi_ca_bi_tu_choi()
    {
        ErrorsOf(new AssignExamRequest()).Should().Contain(message => message.Contains("ít nhất một lớp"));
    }

    [Fact]
    public void Giao_rieng_tung_hoc_sinh_bi_tu_choi()
    {
        // Bài kiểm tra chỉ giao theo lớp. Chỉ danh sách em, không có lớp nào: cả hai lỗi cùng hiện.
        var request = new AssignExamRequest { StudentIds = [11, 12] };

        ErrorsOf(request).Should().Contain(message => message.Contains("chỉ giao theo lớp"));
        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Gui_kem_danh_sach_hoc_sinh_cung_voi_lop_cung_bi_tu_choi()
    {
        // Không được lặng lẽ bỏ qua danh sách em rồi báo "đã giao": đó là cái bẫy mà quy tắc này sinh ra để chặn.
        var request = new AssignExamRequest { GroupIds = [7], StudentIds = [11] };

        ErrorsOf(request).Should().Contain(message => message.Contains("chỉ giao theo lớp"));
    }

    [Fact]
    public void Danh_sach_hoc_sinh_rong_hoac_null_khong_phai_la_loi()
    {
        _validator.Validate(new AssignExamRequest { GroupIds = [7], StudentIds = [] }).IsValid.Should().BeTrue();
        _validator.Validate(new AssignExamRequest { GroupIds = [7], StudentIds = null! }).IsValid.Should().BeTrue();
    }
}
