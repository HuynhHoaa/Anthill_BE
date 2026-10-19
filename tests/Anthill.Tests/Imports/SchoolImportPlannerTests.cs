using Anthill.Application.Common;
using Anthill.Application.Interfaces.Files;
using FluentAssertions;

namespace Anthill.Tests.Imports;

/// <summary>
/// Nhập tổng hợp từ một trang Excel (mỗi dòng một học sinh, kèm lớp và giáo viên chủ nhiệm): bộ lập kế hoạch chia các dòng thành
/// danh sách giáo viên, lớp và học sinh. Giáo viên và lớp suy ra từ chính các dòng, nên mọi xung đột phải bị bắt ở đây, chỉ ra đúng
/// dòng, và không để một lớp thiếu chủ nhiệm lọt sang bước tạo lớp.
/// </summary>
public class SchoolImportPlannerTests
{
    private static SpreadsheetRow Row(
        int number,
        string code,
        string name,
        string group,
        string teacherName = "",
        string teacherEmail = "",
        string teacherPhone = "",
        string subject = "",
        string term = "") =>
        new(number, new Dictionary<string, string>
        {
            ["code"] = code,
            ["fullName"] = name,
            ["group"] = group,
            ["teacherName"] = teacherName,
            ["teacherEmail"] = teacherEmail,
            ["teacherPhone"] = teacherPhone,
            ["subject"] = subject,
            ["email"] = string.Empty,
            ["phone"] = string.Empty,
            ["username"] = string.Empty,
            ["term"] = term
        });

    [Fact]
    public void Ky_hoc_ghi_mot_lan_o_mot_dong_cua_lop_va_ap_cho_ca_lop()
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(2, "A1", "An", "10A1", "Lan", "lan@truong.vn", "0912345678", term: "Học kỳ 1 · 2026–2027"),
            Row(3, "A2", "Bình", "10A1"),
            Row(4, "B1", "Chi", "10A2", "Mai", "mai@truong.vn", "0987654321")
        ]);

        plan.Issues.Should().BeEmpty();
        plan.Groups.Single(group => group.Name == "10A1").Term.Should().Be("Học kỳ 1 · 2026–2027");
        plan.Groups.Single(group => group.Name == "10A1").Row.Cells["term"].Should().Be("Học kỳ 1 · 2026–2027");
        plan.Groups.Single(group => group.Name == "10A2").Term.Should().BeEmpty("lớp không ghi kỳ thì chưa phân kỳ");
    }

    [Fact]
    public void Ky_hoc_lap_lai_giong_nhau_khong_phan_biet_hoa_thuong_va_dau_thi_khong_bao_loi()
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(2, "A1", "An", "10A1", "Lan", "lan@truong.vn", "0912345678", term: "Học kỳ 1"),
            Row(3, "A2", "Bình", "10A1", term: "HOC KY 1")
        ]);

        plan.Issues.Should().BeEmpty();
        plan.Groups.Should().ContainSingle().Which.Term.Should().Be("Học kỳ 1", "giữ cách viết của lần ghi đầu tiên");
    }

    [Fact]
    public void Hai_ky_hoc_khac_nhau_trong_mot_lop_la_xung_dot_va_dong_den_sau_bi_tu_choi()
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(2, "A1", "An", "10A1", "Lan", "lan@truong.vn", "0912345678", term: "Học kỳ 1"),
            Row(3, "A2", "Bình", "10A1", term: "Học kỳ 2")
        ]);

        var issue = plan.Issues.Should().ContainSingle().Subject;
        issue.Row.Should().Be(3);
        issue.Column.Should().Be("Kỳ học");
        issue.Message.Should().Contain("dòng 2").And.Contain("Học kỳ 2");
        plan.Students.Select(student => student.Row).Should().Equal(2);
    }

    [Fact]
    public void Giao_vien_chu_nhiem_chi_can_ghi_o_dong_dau_cua_lop_cac_dong_sau_de_trong()
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(2, "10A101", "An", "10A1", "Nguyễn Văn Lan", "lan@truong.vn", "0912345678", "Toán"),
            Row(3, "10A102", "Bình", "10A1"),
            Row(4, "10A103", "Chi", "10A1")
        ]);

        plan.Issues.Should().BeEmpty();
        plan.Teachers.Should().ContainSingle();
        plan.Groups.Should().ContainSingle();
        plan.Students.Select(student => student.Row).Should().Equal(2, 3, 4);
        plan.Groups[0].TeacherEmail.Should().Be("lan@truong.vn");
    }

    [Fact]
    public void Ghi_lap_lai_dung_giao_vien_o_moi_dong_van_la_mot_giao_vien_mot_lop()
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(2, "A1", "An", "10A1", "Nguyễn Văn Lan", "Lan@Truong.vn", "0912345678"),
            Row(3, "A2", "Bình", "10A1", "nguyen van lan", "lan@truong.vn", "0912 345 678")
        ]);

        plan.Issues.Should().BeEmpty();
        plan.Teachers.Should().ContainSingle();
        plan.Teachers[0].Email.Should().Be("lan@truong.vn", "email không phân biệt hoa thường");
        plan.Students.Should().HaveCount(2);
    }

    [Fact]
    public void So_dong_cua_giao_vien_va_lop_la_dong_dau_tien_xuat_hien_de_loi_tro_dung_cho_nguoi_dung()
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(5, "A1", "An", "10A1", "Nguyễn Văn Lan", "lan@truong.vn", "0912345678"),
            Row(6, "B1", "Bình", "10A2", "Trần Thị Mai", "mai@truong.vn", "0987654321"),
            Row(7, "A2", "Chi", "10A1")
        ]);

        plan.Groups.Select(group => (group.Name, group.FirstRow)).Should().Equal(("10A1", 5), ("10A2", 6));
        plan.Groups.Select(group => group.Row.Number).Should().Equal(5, 6);
        plan.Teachers.Select(teacher => (teacher.Email, teacher.FirstRow)).Should().Equal(("lan@truong.vn", 5), ("mai@truong.vn", 6));
    }

    [Fact]
    public void Ten_lop_khac_hoa_thuong_hoac_dau_van_la_mot_lop_va_hoc_sinh_mang_ten_chuan()
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(2, "A1", "An", "Lớp 10A1", "Lan", "lan@truong.vn", "0912345678"),
            Row(3, "A2", "Bình", "lop 10a1")
        ]);

        plan.Groups.Should().ContainSingle();
        plan.Students.Select(student => student.Source.Cells["group"]).Should().AllBe("Lớp 10A1");
    }

    [Fact]
    public void Mot_giao_vien_chu_nhiem_hai_lop_chi_tao_mot_tai_khoan()
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(2, "A1", "An", "10A1", "Lan", "lan@truong.vn", "0912345678"),
            Row(3, "B1", "Bình", "10A2", "Lan", "lan@truong.vn", "0912345678")
        ]);

        plan.Issues.Should().BeEmpty();
        plan.Teachers.Should().ContainSingle();
        plan.Groups.Should().HaveCount(2);
    }

    [Fact]
    public void Hai_giao_vien_khac_nhau_o_cung_mot_lop_thi_dong_den_sau_bi_tu_choi_va_khong_vao_danh_sach()
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(2, "A1", "An", "10A1", "Lan", "lan@truong.vn", "0912345678"),
            Row(3, "A2", "Bình", "10A1", "Mai", "mai@truong.vn", "0987654321"),
            Row(4, "A3", "Chi", "10A1")
        ]);

        var issue = plan.Issues.Should().ContainSingle().Subject;
        issue.Row.Should().Be(3);
        issue.Column.Should().Be("Email giáo viên");
        issue.Message.Should().Contain("dòng 2").And.Contain("mai@truong.vn");
        plan.Students.Select(student => student.Row).Should().Equal(2, 4);
        plan.Teachers.Should().ContainSingle("giáo viên của dòng bị từ chối không được tạo");
    }

    [Theory]
    [InlineData("Trần Thị Mai", "0912345678", "", "Giáo viên chủ nhiệm")]
    [InlineData("Nguyễn Văn Lan", "0999999999", "", "Số điện thoại giáo viên")]
    [InlineData("Nguyễn Văn Lan", "0912345678", "Văn", "Môn giảng dạy")]
    public void Cung_mot_email_ma_ten_so_dien_thoai_hay_mon_khac_nhau_thi_dong_den_sau_bi_tu_choi(
        string name, string phone, string subject, string expectedColumn)
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(2, "A1", "An", "10A1", "Nguyễn Văn Lan", "lan@truong.vn", "0912345678", "Toán"),
            Row(3, "B1", "Bình", "10A2", name, "lan@truong.vn", phone, subject.Length == 0 ? "Toán" : subject)
        ]);

        var issue = plan.Issues.Should().ContainSingle().Subject;
        issue.Row.Should().Be(3);
        issue.Column.Should().Be(expectedColumn);
        issue.Message.Should().Contain("dòng 2");
        plan.Students.Select(student => student.Row).Should().Equal(2);
    }

    [Fact]
    public void Lop_khong_dong_nao_ghi_giao_vien_thi_bao_mot_loi_goc_va_cac_hoc_sinh_con_lai_la_chua_duoc_xep()
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(2, "A1", "An", "10A1"),
            Row(3, "A2", "Bình", "10A1"),
            Row(4, "B1", "Chi", "10A2", "Mai", "mai@truong.vn", "0987654321")
        ]);

        plan.Groups.Should().ContainSingle(group => group.Name == "10A2");
        plan.Teachers.Should().ContainSingle();
        plan.Students.Select(student => student.Row).Should().Equal(4);

        var root = plan.Issues.Single(issue => issue.Row == 2);
        root.Column.Should().Be("Email giáo viên");
        root.Message.Should().Contain("10A1").And.Contain("chưa có giáo viên chủ nhiệm");

        var dependent = plan.Issues.Single(issue => issue.Row == 3);
        dependent.Column.Should().Be("Lớp");
        dependent.Message.Should().Contain("xem dòng 2");
        plan.Issues.Should().HaveCount(2, "dòng gốc không bị báo hai lần");
    }

    [Fact]
    public void Dong_khong_ghi_lop_bi_tu_choi_ngay()
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(2, "A1", "An", "", "Lan", "lan@truong.vn", "0912345678"),
            Row(3, "A2", "Bình", "10A1", "Lan", "lan@truong.vn", "0912345678")
        ]);

        var issue = plan.Issues.Should().ContainSingle().Subject;
        issue.Row.Should().Be(2);
        issue.Column.Should().Be("Lớp");
        plan.Students.Select(student => student.Row).Should().Equal(3);
    }

    [Fact]
    public void Ho_so_giao_vien_lay_gia_tri_dau_tien_co_du_lieu_du_dong_dau_de_trong_ten_va_so()
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(2, "A1", "An", "10A1", teacherEmail: "lan@truong.vn"),
            Row(3, "A2", "Bình", "10A1", "Nguyễn Văn Lan", "lan@truong.vn", "912345678", "Toán")
        ]);

        plan.Issues.Should().BeEmpty();
        var teacher = plan.Teachers.Should().ContainSingle().Subject;
        teacher.FirstRow.Should().Be(2);
        teacher.Row.Cells["fullName"].Should().Be("Nguyễn Văn Lan");
        teacher.Row.Cells["phone"].Should().Be("0912345678", "số điện thoại Excel làm mất số 0 đầu được thêm lại");
        teacher.Row.Cells["subject"].Should().Be("Toán");
    }

    [Fact]
    public void Lop_chi_co_dong_bi_tu_choi_thi_khong_duoc_tao_va_giao_vien_cua_dong_do_cung_khong()
    {
        var plan = SchoolImportPlanner.Build(
        [
            Row(2, "A1", "An", "10A1", "Nguyễn Văn Lan", "lan@truong.vn", "0912345678"),
            Row(3, "B1", "Bình", "10A2", "Tên Khác Hẳn", "lan@truong.vn", "0912345678")
        ]);

        plan.Issues.Should().ContainSingle(issue => issue.Row == 3);
        plan.Groups.Select(group => group.Name).Should().Equal("10A1");
        plan.Students.Select(student => student.Row).Should().Equal(2);
    }

    [Fact]
    public void Khong_co_dong_nao_thi_ke_hoach_rong()
    {
        var plan = SchoolImportPlanner.Build([]);

        plan.Teachers.Should().BeEmpty();
        plan.Groups.Should().BeEmpty();
        plan.Students.Should().BeEmpty();
        plan.Issues.Should().BeEmpty();
    }
}
