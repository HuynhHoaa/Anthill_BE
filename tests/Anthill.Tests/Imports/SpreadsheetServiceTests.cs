using System.IO.Compression;
using Anthill.Application.Interfaces.Files;
using Anthill.Infrastructure.Spreadsheets;
using ClosedXML.Excel;
using FluentAssertions;

namespace Anthill.Tests.Imports;

/// <summary>
/// Bo doc / dung file Excel. File tai len la du lieu khong tin cay: phai tu choi file xau bang cau tieng Viet (khong nem
/// ngoai le cua thu vien), khong bao gio tinh cong thuc, va doc dung du lieu nguoi dung that su co — tieu de nam o dong
/// nao cung duoc, ten cot go kieu nao cung nhan ra, o so mat so 0 dau van doc dung.
/// </summary>
public class SpreadsheetServiceTests
{
    private static readonly IReadOnlyList<SpreadsheetColumn> Columns =
    [
        new("code", "Mã học sinh", true, ["MSHS", "Mã HS"]),
        new("fullName", "Họ và tên", true, ["Họ tên"]),
        new("group", "Lớp", false, ["Tên lớp"]),
        new("phone", "Số điện thoại", false, ["SĐT"])
    ];

    private readonly ClosedXmlSpreadsheetService _service = new();

    private static MemoryStream Workbook(Action<IXLWorksheet> fill, string sheetName = "Học sinh", Action<XLWorkbook>? more = null)
    {
        using var book = new XLWorkbook();
        fill(book.Worksheets.Add(sheetName));
        more?.Invoke(book);

        var stream = new MemoryStream();
        book.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static void Header(IXLWorksheet sheet, int row, params string[] titles)
    {
        for (var index = 0; index < titles.Length; index++)
        {
            sheet.Cell(row, index + 1).Value = titles[index];
        }
    }

    // ── Doc ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Doc_dung_cac_dong_va_giu_so_dong_that_trong_Excel()
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, "Mã học sinh*", "Họ và tên*", "Lớp");
            sheet.Cell(2, 1).Value = "HS001";
            sheet.Cell(2, 2).Value = "Nguyễn Văn An";
            sheet.Cell(2, 3).Value = "10A1";
            sheet.Cell(3, 1).Value = "HS002";
            sheet.Cell(3, 2).Value = "Trần Thị Bình";
        });

        var result = _service.Read(stream, Columns, maxRows: 100);

        result.IsSuccess.Should().BeTrue();
        var rows = result.ValueOrThrow.Rows;
        rows.Should().HaveCount(2);
        rows[0].Number.Should().Be(2);
        rows[0].Cells["code"].Should().Be("HS001");
        rows[0].Cells["fullName"].Should().Be("Nguyễn Văn An");
        rows[0].Cells["group"].Should().Be("10A1");
        rows[1].Number.Should().Be(3);
        rows[1].Cells["group"].Should().BeEmpty();
    }

    [Fact]
    public void Tieu_de_o_dong_bat_ky_trong_muoi_dong_dau_van_nhan_ra()
    {
        using var stream = Workbook(sheet =>
        {
            sheet.Cell(1, 1).Value = "DANH SÁCH HỌC SINH KHỐI 10";
            sheet.Cell(2, 1).Value = "Năm học 2026–2027";
            Header(sheet, 4, "Mã học sinh", "Họ và tên");
            sheet.Cell(5, 1).Value = "HS001";
            sheet.Cell(5, 2).Value = "Nguyễn Văn An";
        });

        var result = _service.Read(stream, Columns, 100);

        result.IsSuccess.Should().BeTrue();
        result.ValueOrThrow.Rows.Should().ContainSingle().Which.Number.Should().Be(5);
    }

    [Theory]
    [InlineData("MÃ HỌC SINH *", "HỌ VÀ TÊN *")]
    [InlineData("ma hoc sinh", "ho va ten")]
    [InlineData("Mã học sinh*", "Họ tên")]
    [InlineData("MSHS", "Họ tên")]
    [InlineData("  Mã   HS  ", "ho  và  tên")]
    public void Ten_cot_go_kieu_nao_cung_nhan_ra(string codeTitle, string nameTitle)
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, codeTitle, nameTitle);
            sheet.Cell(2, 1).Value = "HS001";
            sheet.Cell(2, 2).Value = "Nguyễn Văn An";
        });

        var result = _service.Read(stream, Columns, 100);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.ValueOrThrow.Rows.Should().ContainSingle().Which.Cells["code"].Should().Be("HS001");
    }

    [Fact]
    public void Cot_thua_bi_bo_qua_va_dong_trong_bi_bo_qua()
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, "Ghi chú riêng", "Mã học sinh", "Họ và tên", "Cột lạ");
            sheet.Cell(2, 1).Value = "bỏ qua";
            sheet.Cell(2, 2).Value = "HS001";
            sheet.Cell(2, 3).Value = "Nguyễn Văn An";

            // dòng 3 trống hoàn toàn
            sheet.Cell(4, 2).Value = "HS002";
            sheet.Cell(4, 3).Value = "Trần Thị Bình";
        });

        var result = _service.Read(stream, Columns, 100);

        result.IsSuccess.Should().BeTrue();
        result.ValueOrThrow.Rows.Select(row => row.Number).Should().Equal(2, 4);
        result.ValueOrThrow.Rows[0].Cells.Keys.Should().BeEquivalentTo("code", "fullName");
    }

    /// <summary>
    /// Một ô lạc ở dòng rất xa (gõ nhầm, hay Ctrl+End thấy "hết" ở đó) không được kéo bộ đọc đi duyệt từng dòng rỗng: nó chỉ
    /// được đọc những dòng có nội dung, và vẫn báo đúng số dòng thật.
    /// </summary>
    [Fact]
    public void O_lac_o_dong_rat_xa_khong_lam_bo_doc_duyet_tung_dong_trong()
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, "Mã học sinh", "Họ và tên");
            sheet.Cell(2, 1).Value = "HS001";
            sheet.Cell(2, 2).Value = "Nguyễn Văn An";
            sheet.Cell(1_000_000, 1).Value = "HS999";
            sheet.Cell(1_000_000, 2).Value = "Người cuối";
        });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = _service.Read(stream, Columns, maxRows: 100);
        stopwatch.Stop();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.ValueOrThrow.Rows.Select(row => row.Number).Should().Equal(2, 1_000_000);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Trường hay gộp ô cột "Lớp" (và cột giáo viên) cho cả nhóm dòng: Excel chỉ giữ giá trị ở ô trên cùng bên trái, các ô còn lại của
    /// vùng gộp đọc ra rỗng. Mỗi dòng trong vùng gộp phải nhận giá trị của vùng, còn dòng ngoài vùng thì không.
    /// </summary>
    [Fact]
    public void O_gop_ap_gia_tri_cho_moi_dong_trong_vung_gop_va_khong_tran_ra_ngoai()
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, "Mã học sinh", "Họ và tên", "Lớp");
            for (var row = 2; row <= 8; row++)
            {
                sheet.Cell(row, 1).Value = $"HS{row:000}";
                sheet.Cell(row, 2).Value = $"Học sinh {row}";
            }

            sheet.Cell(2, 3).Value = "Lớp 6A";
            sheet.Range(2, 3, 4, 3).Merge();
            sheet.Cell(5, 3).Value = "Lớp 6B";
            sheet.Range(5, 3, 7, 3).Merge();

            // Dòng 8 không thuộc vùng gộp nào: phải để trống, không được thừa hưởng lớp của vùng ngay trên.
        });

        var result = _service.Read(stream, Columns, maxRows: 100);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.ValueOrThrow.Rows.Select(row => row.Cells["group"]).Should()
            .Equal("Lớp 6A", "Lớp 6A", "Lớp 6A", "Lớp 6B", "Lớp 6B", "Lớp 6B", string.Empty);
    }

    [Fact]
    public void O_gop_chi_ap_cho_cot_dang_doc_va_dong_ben_duoi_tieu_de()
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, "Mã học sinh", "Họ và tên");
            sheet.Cell(2, 1).Value = "HS001";
            sheet.Cell(2, 2).Value = "Nguyễn Văn An";
            sheet.Cell(3, 1).Value = "HS002";
            sheet.Cell(3, 2).Value = "Trần Thị Bình";

            // Cột ngoài danh sách cột cần đọc có gộp ô: bỏ qua, không làm hỏng việc đọc.
            sheet.Cell(2, 6).Value = "Ghi chú chung";
            sheet.Range(2, 6, 3, 6).Merge();
        });

        var result = _service.Read(stream, Columns, maxRows: 100);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.ValueOrThrow.Rows.Should().HaveCount(2);
        result.ValueOrThrow.Rows[1].Cells.Keys.Should().BeEquivalentTo("code", "fullName");
    }

    [Fact]
    public void Vung_gop_keo_dai_ca_trieu_dong_khong_lam_treo_bo_doc()
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, "Mã học sinh", "Họ và tên", "Lớp");
            sheet.Cell(2, 1).Value = "HS001";
            sheet.Cell(2, 2).Value = "Nguyễn Văn An";
            sheet.Cell(3, 1).Value = "HS002";
            sheet.Cell(3, 2).Value = "Trần Thị Bình";
            sheet.Cell(2, 3).Value = "Lớp 6A";
            sheet.Range(2, 3, 1_000_000, 3).Merge();
        });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = _service.Read(stream, Columns, maxRows: 100);
        stopwatch.Stop();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.ValueOrThrow.Rows.Select(row => row.Cells["group"]).Should().Equal("Lớp 6A", "Lớp 6A");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void O_so_in_dung_nhu_go_ma_hoc_sinh_va_so_dien_thoai_khong_thanh_ky_hieu_khoa_hoc()
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, "Mã học sinh", "Họ và tên", "Số điện thoại");
            sheet.Cell(2, 1).Value = 10001;
            sheet.Cell(2, 2).Value = "Nguyễn Văn An";

            // Excel nuốt số 0 đầu của ô kiểu số: 0912345678 chỉ còn 912345678.
            sheet.Cell(2, 3).Value = 912345678;
            sheet.Cell(3, 1).Value = 123456789012;
            sheet.Cell(3, 2).Value = "Trần Thị Bình";
            sheet.Cell(3, 3).Value = 3.5;
        });

        var result = _service.Read(stream, Columns, 100);

        var rows = result.ValueOrThrow.Rows;
        rows[0].Cells["code"].Should().Be("10001");
        rows[0].Cells["phone"].Should().Be("912345678");
        rows[1].Cells["code"].Should().Be("123456789012");
        rows[1].Cells["phone"].Should().Be("3.5");
    }

    [Fact]
    public void Chuoi_tieng_Viet_to_hop_NFD_duoc_dua_ve_NFC()
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, "Mã học sinh", "Họ và tên");
            sheet.Cell(2, 1).Value = "HS001";
            sheet.Cell(2, 2).Value = "Nguyễn Văn An";
        });

        var result = _service.Read(stream, Columns, 100);

        result.ValueOrThrow.Rows[0].Cells["fullName"].Should().Be("Nguyễn Văn An");
    }

    [Fact]
    public void Khoang_trang_khong_ngat_va_ky_tu_vo_hinh_duoc_don_sach()
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, "Mã học sinh", "Họ và tên");
            sheet.Cell(2, 1).Value = "​HS001 ";
            sheet.Cell(2, 2).Value = "﻿  Nguyễn Văn An ";
        });

        var row = _service.Read(stream, Columns, 100).ValueOrThrow.Rows[0];

        row.Cells["code"].Should().Be("HS001");
        row.Cells["fullName"].Should().Be("Nguyễn Văn An");
    }

    [Fact]
    public void Cong_thuc_khong_bao_gio_duoc_tinh()
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, "Mã học sinh", "Họ và tên");
            sheet.Cell(2, 1).Value = "HS001";

            // Công thức chưa có giá trị đã lưu: coi như ô trống. Không được chạy nó để lấy kết quả.
            sheet.Cell(2, 2).FormulaA1 = "=1+1";
        });

        var row = _service.Read(stream, Columns, 100).ValueOrThrow.Rows.Single();

        row.Cells["fullName"].Should().BeEmpty();
    }

    [Fact]
    public void Trang_an_khong_duoc_doc()
    {
        using var stream = Workbook(
            sheet => sheet.Cell(1, 1).Value = "Trang đầu không có gì",
            more: book =>
            {
                var hidden = book.Worksheets.Add("Ẩn");
                Header(hidden, 1, "Mã học sinh", "Họ và tên");
                hidden.Cell(2, 1).Value = "HS001";
                hidden.Cell(2, 2).Value = "Nguyễn Văn An";
                hidden.Visibility = XLWorksheetVisibility.Hidden;
            });

        var result = _service.Read(stream, Columns, 100);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Lay_trang_dau_tien_co_du_cot_bat_buoc_ke_ca_khi_khong_phai_trang_dau()
    {
        using var stream = Workbook(
            sheet => sheet.Cell(1, 1).Value = "Hướng dẫn: điền dữ liệu ở trang sau",
            "Hướng dẫn",
            book =>
            {
                var data = book.Worksheets.Add("Dữ liệu");
                Header(data, 1, "Mã học sinh", "Họ và tên");
                data.Cell(2, 1).Value = "HS001";
                data.Cell(2, 2).Value = "Nguyễn Văn An";
            });

        var result = _service.Read(stream, Columns, 100);

        result.IsSuccess.Should().BeTrue();
        result.ValueOrThrow.Rows.Should().ContainSingle();
    }

    // ── Tu choi ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Thieu_cot_bat_buoc_thi_noi_ro_cot_nao()
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, "Họ và tên", "Lớp");
            sheet.Cell(2, 1).Value = "Nguyễn Văn An";
        });

        var result = _service.Read(stream, Columns, 100);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Không tìm thấy cột bắt buộc").And.Contain("Mã học sinh");
        result.Error.Should().NotContain("Họ và tên");
    }

    [Fact]
    public void File_khong_co_gi_thi_noi_khong_co_du_lieu()
    {
        using var stream = Workbook(_ => { });

        var result = _service.Read(stream, Columns, 100);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("không có dữ liệu");
    }

    [Fact]
    public void Qua_so_dong_toi_da_thi_tu_choi_ca_file()
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, "Mã học sinh", "Họ và tên");
            for (var row = 2; row <= 12; row++)
            {
                sheet.Cell(row, 1).Value = $"HS{row:000}";
                sheet.Cell(row, 2).Value = "Nguyễn Văn An";
            }
        });

        var result = _service.Read(stream, Columns, maxRows: 10);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("hơn 10 dòng");
    }

    [Fact]
    public void Dung_so_dong_toi_da_thi_van_doc()
    {
        using var stream = Workbook(sheet =>
        {
            Header(sheet, 1, "Mã học sinh", "Họ và tên");
            for (var row = 2; row <= 11; row++)
            {
                sheet.Cell(row, 1).Value = $"HS{row:000}";
                sheet.Cell(row, 2).Value = "Nguyễn Văn An";
            }
        });

        _service.Read(stream, Columns, maxRows: 10).ValueOrThrow.Rows.Should().HaveCount(10);
    }

    [Fact]
    public void File_van_ban_khong_phai_Excel_thi_bao_bang_cau_tieng_Viet()
    {
        using var stream = new MemoryStream("đây không phải Excel"u8.ToArray());

        var result = _service.Read(stream, Columns, 100);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("không phải Excel");
    }

    [Fact]
    public void Goi_zip_khong_co_workbook_thi_khong_phai_Excel()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("ghi-chu.txt").Open());
            writer.Write("zip thường");
        }

        stream.Position = 0;
        var result = _service.Read(stream, Columns, 100);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("không phải Excel");
    }

    [Fact]
    public void Bom_zip_nen_nho_nhung_phinh_to_khi_giai_nen_bi_chan_truoc_khi_dua_cho_thu_vien()
    {
        using var valid = Workbook(sheet =>
        {
            Header(sheet, 1, "Mã học sinh", "Họ và tên");
            sheet.Cell(2, 1).Value = "HS001";
            sheet.Cell(2, 2).Value = "Nguyễn Văn An";
        });

        using var bomb = new MemoryStream();
        using (var source = new ZipArchive(valid, ZipArchiveMode.Read))
        using (var target = new ZipArchive(bomb, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                using var from = entry.Open();
                using var to = target.CreateEntry(entry.FullName, CompressionLevel.Optimal).Open();
                from.CopyTo(to);
            }

            // 120 MB toàn số 0 nén còn vài trăm KB.
            using var junk = target.CreateEntry("xl/junk.bin", CompressionLevel.Optimal).Open();
            var chunk = new byte[1024 * 1024];
            for (var index = 0; index < 120; index++)
            {
                junk.Write(chunk);
            }
        }

        bomb.Length.Should().BeLessThan(5 * 1024 * 1024);
        bomb.Position = 0;

        var result = _service.Read(bomb, Columns, 100);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("quá lớn sau khi giải nén");
    }

    // ── Dung file mau ─────────────────────────────────────────────────────────────────────────

    private static SpreadsheetTemplate Template(IReadOnlyList<string>? choices = null) => new(
        "Học sinh",
        [
            new TemplateColumn("Mã học sinh*", 16),
            new TemplateColumn("Họ và tên*", 30),
            new TemplateColumn("Lớp", 22, choices),
            new TemplateColumn("Số điện thoại", 16)
        ],
        ["Hướng dẫn", "Bước một", "Bước hai"],
        MaxRows: 5000);

    [Fact]
    public void File_mau_co_trang_nhap_lieu_trang_danh_sach_va_trang_huong_dan()
    {
        var bytes = _service.BuildTemplate(Template(["10A1", "10A2", "11B"]));

        using var book = new XLWorkbook(new MemoryStream(bytes));

        book.Worksheets.Select(sheet => sheet.Name).Should().Equal("Học sinh", "Danh sách", "Hướng dẫn");
        var data = book.Worksheet("Học sinh");
        data.Cell(1, 1).GetString().Should().Be("Mã học sinh*");
        data.Cell(1, 3).GetString().Should().Be("Lớp");
        data.Cell(1, 1).Style.Font.Bold.Should().BeTrue();

        var list = book.Worksheet("Danh sách");
        list.Cell(1, 1).GetString().Should().Be("Lớp");
        list.Cell(2, 1).GetString().Should().Be("10A1");
        list.Cell(4, 1).GetString().Should().Be("11B");

        book.Worksheet("Hướng dẫn").Cell(3, 1).GetString().Should().Be("Bước hai");
    }

    [Fact]
    public void File_mau_khong_co_cot_chon_thi_khong_co_trang_danh_sach()
    {
        var bytes = _service.BuildTemplate(Template());

        using var book = new XLWorkbook(new MemoryStream(bytes));

        book.Worksheets.Select(sheet => sheet.Name).Should().Equal("Học sinh", "Hướng dẫn");
    }

    [Fact]
    public void Cot_ma_va_so_dien_thoai_trong_file_mau_dinh_dang_van_ban_de_khong_nuot_so_0_dau()
    {
        var bytes = _service.BuildTemplate(Template());

        using var book = new XLWorkbook(new MemoryStream(bytes));
        var data = book.Worksheet("Học sinh");

        data.Column(1).Style.NumberFormat.Format.Should().Be("@");
        data.Column(4).Style.NumberFormat.Format.Should().Be("@");
    }

    [Fact]
    public void File_mau_dien_xong_doc_lai_duoc_ngay_ke_ca_dau_sao_o_tieu_de()
    {
        var bytes = _service.BuildTemplate(Template(["10A1"]));

        using var book = new XLWorkbook(new MemoryStream(bytes));
        var data = book.Worksheet("Học sinh");
        data.Cell(2, 1).Value = "0012";
        data.Cell(2, 2).Value = "Nguyễn Văn An";
        data.Cell(2, 3).Value = "10A1";
        data.Cell(2, 4).Value = "0912345678";

        var filled = new MemoryStream();
        book.SaveAs(filled);
        filled.Position = 0;

        var row = _service.Read(filled, Columns, 100).ValueOrThrow.Rows.Single();

        row.Number.Should().Be(2);
        row.Cells["code"].Should().Be("0012");
        row.Cells["phone"].Should().Be("0912345678");
        row.Cells["group"].Should().Be("10A1");
    }

    // ── Dung bang xuat (bang diem) ────────────────────────────────────────────────────────────

    private static SpreadsheetTable Table(
        IReadOnlyList<IReadOnlyList<object?>> rows,
        IReadOnlyList<IReadOnlyList<object?>>? footer = null,
        string sheetName = "Bảng điểm",
        int frozenColumns = 0) => new(
        sheetName,
        ["BẢNG ĐIỂM LỚP 10A1", "Giáo viên: Nguyễn Văn An"],
        [
            new TableColumn("STT", 6, Centered: true),
            new TableColumn("Họ và tên", 28),
            new TableColumn("Điểm", 10, Centered: true)
        ],
        rows,
        footer ?? [],
        frozenColumns);

    private static XLWorkbook Open(byte[] bytes) => new(new MemoryStream(bytes));

    [Fact]
    public void Bang_xuat_co_tieu_de_dong_trong_hang_dau_cot_roi_den_du_lieu()
    {
        var bytes = _service.BuildTable(Table([[1, "Nguyễn Văn An", 8.5m], [2, "Trần Thị Bình", null]]));

        using var book = Open(bytes);
        var sheet = book.Worksheet("Bảng điểm");

        sheet.Cell(1, 1).GetString().Should().Be("BẢNG ĐIỂM LỚP 10A1");
        sheet.Cell(1, 1).Style.Font.Bold.Should().BeTrue();
        sheet.Cell(2, 1).GetString().Should().Be("Giáo viên: Nguyễn Văn An");
        sheet.Cell(3, 1).IsEmpty().Should().BeTrue("một dòng trống ngăn tiêu đề với bảng");
        sheet.Cell(4, 2).GetString().Should().Be("Họ và tên");
        sheet.Cell(4, 2).Style.Font.Bold.Should().BeTrue();
        sheet.Cell(5, 2).GetString().Should().Be("Nguyễn Văn An");
        sheet.Cell(6, 2).GetString().Should().Be("Trần Thị Bình");
        sheet.Cell(6, 3).IsEmpty().Should().BeTrue("ô null để trống chứ không ghi số 0");
    }

    [Fact]
    public void So_duoc_ghi_la_so_de_excel_tinh_trung_binh_duoc()
    {
        var bytes = _service.BuildTable(Table([[1, "An", 8.5m], [2, "Bình", 7.25m]]));

        using var book = Open(bytes);
        var sheet = book.Worksheet("Bảng điểm");

        sheet.Cell(5, 1).Value.IsNumber.Should().BeTrue();
        sheet.Cell(5, 3).Value.IsNumber.Should().BeTrue();
        sheet.Cell(5, 3).Value.GetNumber().Should().Be(8.5);
        sheet.Cell(5, 3).Style.NumberFormat.Format.Should().Be("0.00");
        sheet.Cell(6, 3).Value.GetNumber().Should().Be(7.25);
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+SUM(A1:A9)")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    [InlineData("=HYPERLINK(\"http://x\",\"bấm vào\")")]
    public void Chuoi_bat_dau_bang_ky_tu_cong_thuc_van_la_van_ban_khong_bao_gio_la_cong_thuc(string hostile)
    {
        // Tên học sinh và tên đề do người dùng gõ: mở file bằng Excel không được chạy gì từ đó.
        var bytes = _service.BuildTable(Table([[1, hostile, 5m]]));

        using var book = Open(bytes);
        var cell = book.Worksheet("Bảng điểm").Cell(5, 2);

        cell.HasFormula.Should().BeFalse();
        cell.Value.IsText.Should().BeTrue();
        cell.GetString().Should().Be(hostile);
    }

    [Fact]
    public void Dong_tong_in_dam_va_nam_ngay_duoi_du_lieu()
    {
        var bytes = _service.BuildTable(Table([[1, "An", 8m]], footer: [[null, "Trung bình lớp", 8m]]));

        using var book = Open(bytes);
        var sheet = book.Worksheet("Bảng điểm");

        sheet.Cell(6, 2).GetString().Should().Be("Trung bình lớp");
        sheet.Cell(6, 2).Style.Font.Bold.Should().BeTrue();
        sheet.Cell(6, 3).Value.GetNumber().Should().Be(8);
        sheet.Cell(5, 2).Style.Font.Bold.Should().BeFalse();
    }

    [Fact]
    public void Hang_dau_cot_va_cac_cot_dau_duoc_co_dinh_khi_cuon()
    {
        var bytes = _service.BuildTable(Table([[1, "An", 8m]], frozenColumns: 2));

        using var book = Open(bytes);
        var view = book.Worksheet("Bảng điểm").SheetView;

        view.SplitRow.Should().Be(4);
        view.SplitColumn.Should().Be(2);
    }

    [Fact]
    public void Ten_trang_tinh_bi_excel_cam_ky_tu_nao_thi_doi_thanh_dau_cach_va_cat_o_31_ky_tu()
    {
        var bytes = _service.BuildTable(Table([], sheetName: "Lớp 10A1/2: [điểm]? " + new string('x', 40)));

        using var book = Open(bytes);
        var name = book.Worksheets.Single().Name;

        name.Length.Should().BeLessThanOrEqualTo(31);
        name.Should().NotContainAny("\\", "/", "?", "*", "[", "]", ":");
    }

    [Fact]
    public void Bang_trong_van_dung_duoc_chi_co_tieu_de_va_hang_dau_cot()
    {
        var bytes = _service.BuildTable(Table([]));

        using var book = Open(bytes);
        var sheet = book.Worksheet("Bảng điểm");

        sheet.Cell(4, 2).GetString().Should().Be("Họ và tên");
        sheet.Cell(5, 2).IsEmpty().Should().BeTrue();
    }
}
