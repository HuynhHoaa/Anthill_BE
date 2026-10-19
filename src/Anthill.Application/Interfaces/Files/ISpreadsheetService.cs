using Anthill.Application.Common;

namespace Anthill.Application.Interfaces.Files;

/// <summary>Một cột mà bộ đọc phải tìm trong file Excel của người dùng.</summary>
/// <param name="Key">Khoá nội bộ để lấy giá trị ra khỏi <see cref="SpreadsheetRow.Cells"/>.</param>
/// <param name="Header">Tên cột trong file mẫu.</param>
/// <param name="Required">Thiếu cột này thì cả file bị từ chối (chứ không phải từng dòng).</param>
/// <param name="Aliases">Các tên khác người dùng hay gõ ("MSHS" cho "Mã học sinh"...).</param>
public sealed record SpreadsheetColumn(string Key, string Header, bool Required, IReadOnlyList<string> Aliases);

/// <summary>Một dòng dữ liệu đã đọc. <see cref="Number"/> là số dòng trong Excel (bắt đầu từ 1) để báo lỗi đúng chỗ.</summary>
public sealed record SpreadsheetRow(int Number, IReadOnlyDictionary<string, string> Cells);

public sealed record SpreadsheetData(IReadOnlyList<SpreadsheetRow> Rows);

/// <summary>Một cột của file mẫu.</summary>
/// <param name="Header">Tiêu đề cột (cột bắt buộc ghi dấu *).</param>
/// <param name="Width">Độ rộng cột, tính theo ký tự.</param>
/// <param name="Choices">Cho ô chọn sẵn từ danh sách (ví dụ tên lớp đang có); NULL nếu cột nhập tự do.</param>
public sealed record TemplateColumn(string Header, int Width, IReadOnlyList<string>? Choices = null);

/// <summary>File mẫu: một trang nhập liệu và một trang hướng dẫn.</summary>
/// <param name="SheetName">Tên trang nhập liệu.</param>
/// <param name="Columns">Các cột.</param>
/// <param name="GuideLines">Các dòng của trang "Hướng dẫn".</param>
/// <param name="MaxRows">Số dòng tối đa của một lần nhập; ô chọn sẵn phủ tới chừng đó dòng.</param>
public sealed record SpreadsheetTemplate(
    string SheetName,
    IReadOnlyList<TemplateColumn> Columns,
    IReadOnlyList<string> GuideLines,
    int MaxRows);

/// <summary>Một cột của bảng xuất ra Excel.</summary>
/// <param name="Header">Tiêu đề cột.</param>
/// <param name="Width">Độ rộng cột, tính theo ký tự.</param>
/// <param name="Centered">Căn giữa các ô của cột (số thứ tự, điểm); mặc định căn trái.</param>
public sealed record TableColumn(string Header, int Width, bool Centered = false);

/// <summary>Một bảng dữ liệu xuất ra Excel: dòng tiêu đề phía trên, hàng đầu cột, các dòng dữ liệu và vài dòng tổng cuối bảng.</summary>
/// <param name="SheetName">Tên trang tính.</param>
/// <param name="TitleLines">Các dòng chữ phía trên bảng; dòng đầu in đậm, cỡ lớn.</param>
/// <param name="Columns">Các cột.</param>
/// <param name="Rows">Dòng dữ liệu. Mỗi ô là <c>null</c> (để trống), chuỗi, hoặc số (<c>int</c>, <c>decimal</c>, <c>double</c>).</param>
/// <param name="FooterRows">Các dòng tổng (trung bình, sĩ số...) in đậm ngay dưới dữ liệu; cùng kiểu ô với <paramref name="Rows"/>.</param>
/// <param name="FrozenColumns">Số cột đầu cố định khi cuộn ngang (tên học sinh vẫn thấy khi xem tới đề cuối).</param>
public sealed record SpreadsheetTable(
    string SheetName,
    IReadOnlyList<string> TitleLines,
    IReadOnlyList<TableColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    IReadOnlyList<IReadOnlyList<object?>> FooterRows,
    int FrozenColumns = 0);

/// <summary>
/// Đọc và dựng file Excel (.xlsx). Tầng Application chỉ biết cổng này; thư viện thật nằm ở Infrastructure.
/// </summary>
/// <remarks>
/// File do người dùng tải lên là dữ liệu không tin cậy: bộ đọc phải từ chối file nén phình ra quá lớn, không bao giờ tính công
/// thức (chỉ đọc giá trị đã lưu), và trả lỗi tiếng Việt dễ hiểu thay vì ném ngoại lệ của thư viện.
/// </remarks>
public interface ISpreadsheetService
{
    /// <summary>
    /// Đọc trang đầu tiên có đủ các cột bắt buộc. Hàng tiêu đề được dò theo tên cột trong mười dòng đầu (không khớp hoa thường,
    /// dấu hay ký tự *), cột thừa bị bỏ qua, dòng trống bị bỏ qua. Số dòng dữ liệu vượt <paramref name="maxRows"/> trả lỗi.
    /// </summary>
    Result<SpreadsheetData> Read(Stream workbook, IReadOnlyList<SpreadsheetColumn> columns, int maxRows);

    /// <summary>Dựng file mẫu.</summary>
    byte[] BuildTemplate(SpreadsheetTemplate template);

    /// <summary>
    /// Dựng file bảng dữ liệu (bảng điểm...). Chuỗi luôn được ghi là <b>văn bản</b>, không bao giờ thành công thức, dù bắt đầu bằng
    /// <c>=</c>, <c>+</c>, <c>-</c> hay <c>@</c>: tên học sinh và tên đề do người dùng gõ nên không được tin.
    /// </summary>
    byte[] BuildTable(SpreadsheetTable table);
}
