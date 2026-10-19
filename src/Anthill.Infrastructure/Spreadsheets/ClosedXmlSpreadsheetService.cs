using System.Globalization;
using System.IO.Compression;
using System.Text;
using Anthill.Application.Common;
using Anthill.Application.Interfaces.Files;
using ClosedXML.Excel;

namespace Anthill.Infrastructure.Spreadsheets;

/// <summary>
/// <see cref="ISpreadsheetService"/> bằng ClosedXML.
/// </summary>
/// <remarks>
/// <para>
/// <b>File tải lên là dữ liệu không tin cậy.</b> Một file .xlsx là một gói zip: trước khi đưa cho thư viện, ta kiểm số mục và
/// tổng dung lượng sau khi giải nén để chặn "bom zip" (vài MB nén phình ra hàng GB trong bộ nhớ). Công thức không được tính:
/// chỉ đọc giá trị đã lưu cùng file.
/// </para>
/// <para>
/// <b>Chuỗi tiếng Việt từ Excel hay ở dạng tổ hợp (NFD)</b> — "ê" lưu thành "e" cộng dấu rời — nên so khớp tên cột và mọi giá
/// trị đều đi qua chuẩn hoá NFC trước; nếu không, "Lớp 10A" gõ hai kiểu là hai chuỗi khác nhau.
/// </para>
/// </remarks>
public sealed class ClosedXmlSpreadsheetService : ISpreadsheetService
{
    private const long MaxUncompressedBytes = 100L * 1024 * 1024;
    private const int MaxZipEntries = 1000;
    private const int HeaderScanRows = 10;

    /// <summary>Số vùng gộp ô tối đa được xét: file thật chỉ có vài chục, một file cố tình dựng hàng triệu vùng thì bỏ qua phần thừa.</summary>
    private const int MaxMergedRanges = 50_000;

    private const string UnreadableMessage =
        "Không đọc được file. Hãy dùng file mẫu hoặc lưu lại dưới dạng Excel (.xlsx) rồi thử lại.";

    /// <inheritdoc />
    public Result<SpreadsheetData> Read(Stream workbook, IReadOnlyList<SpreadsheetColumn> columns, int maxRows)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(columns);

        using var buffer = new MemoryStream();
        workbook.CopyTo(buffer);
        buffer.Position = 0;

        if (InspectPackage(buffer) is { } packageError)
        {
            return Result.Failure<SpreadsheetData>(packageError);
        }

        buffer.Position = 0;

        try
        {
            using var book = new XLWorkbook(buffer);
            return ReadBook(book, columns, maxRows);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Result.Failure<SpreadsheetData>(UnreadableMessage);
        }
    }

    /// <summary>Kiểm gói zip: đúng là .xlsx, không quá nhiều mục, không phình quá lớn khi giải nén. NULL nghĩa là ổn.</summary>
    private static string? InspectPackage(Stream stream)
    {
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

            if (zip.Entries.Count > MaxZipEntries)
            {
                return "File Excel có quá nhiều thành phần, không đọc được.";
            }

            if (zip.Entries.Sum(entry => entry.Length) > MaxUncompressedBytes)
            {
                return "File Excel quá lớn sau khi giải nén. Hãy bỏ bớt ảnh và định dạng thừa rồi thử lại.";
            }

            return zip.GetEntry("xl/workbook.xml") is null
                ? "File này không phải Excel (.xlsx). Hãy dùng file mẫu hoặc lưu lại dưới dạng .xlsx."
                : null;
        }
        catch (InvalidDataException)
        {
            return "File này không phải Excel (.xlsx). Hãy dùng file mẫu hoặc lưu lại dưới dạng .xlsx.";
        }
    }

    private static Result<SpreadsheetData> ReadBook(XLWorkbook book, IReadOnlyList<SpreadsheetColumn> columns, int maxRows)
    {
        HeaderMatch? best = null;

        foreach (var sheet in book.Worksheets.Where(sheet => sheet.Visibility == XLWorksheetVisibility.Visible))
        {
            var used = sheet.RangeUsed(XLCellsUsedOptions.Contents);
            if (used is null)
            {
                continue;
            }

            var firstRow = used.FirstRow().RowNumber();
            var lastRow = used.LastRow().RowNumber();
            var firstColumn = used.FirstColumn().ColumnNumber();
            var lastColumn = used.LastColumn().ColumnNumber();

            for (var row = firstRow; row <= Math.Min(lastRow, firstRow + HeaderScanRows - 1); row++)
            {
                var match = MatchHeaders(sheet, row, firstColumn, lastColumn, columns);
                if (best is null || match.Found.Count > best.Found.Count)
                {
                    best = match;
                }

                if (match.MissingRequired.Count == 0)
                {
                    return ReadRows(match, maxRows);
                }
            }
        }

        var missing = best?.MissingRequired ?? columns.Where(column => column.Required).Select(column => column.Header).ToList();

        return Result.Failure<SpreadsheetData>(
            best is null
                ? "File không có dữ liệu nào."
                : $"Không tìm thấy cột bắt buộc: {string.Join(", ", missing)}. Hãy dùng file mẫu và giữ nguyên hàng tiêu đề.");
    }

    private sealed record HeaderMatch(
        IXLWorksheet Sheet,
        int HeaderRow,
        IReadOnlyDictionary<string, int> Found,
        IReadOnlyList<string> MissingRequired);

    private static HeaderMatch MatchHeaders(
        IXLWorksheet sheet,
        int row,
        int firstColumn,
        int lastColumn,
        IReadOnlyList<SpreadsheetColumn> columns)
    {
        var lookup = new Dictionary<string, SpreadsheetColumn>();
        foreach (var column in columns)
        {
            lookup[Fold(column.Header)] = column;
            foreach (var alias in column.Aliases)
            {
                lookup[Fold(alias)] = column;
            }
        }

        var found = new Dictionary<string, int>();
        for (var columnNumber = firstColumn; columnNumber <= lastColumn; columnNumber++)
        {
            var text = Fold(CellText(sheet.Cell(row, columnNumber)));
            if (text.Length > 0 && lookup.TryGetValue(text, out var column) && !found.ContainsKey(column.Key))
            {
                found[column.Key] = columnNumber;
            }
        }

        var missing = columns
            .Where(column => column.Required && !found.ContainsKey(column.Key))
            .Select(column => column.Header)
            .ToList();

        return new HeaderMatch(sheet, row, found, missing);
    }

    private static Result<SpreadsheetData> ReadRows(HeaderMatch match, int maxRows)
    {
        var rows = new List<SpreadsheetRow>();
        var merged = MergedSpans(match);

        // Chỉ duyệt những dòng thật sự có nội dung. Duyệt từng số dòng tới ô cuối cùng thì một ô lạc ở dòng 1.000.000 (gõ nhầm,
        // hay Ctrl+End thấy "hết" ở đó) khiến thư viện tạo hàng triệu ô rỗng trong bộ nhớ chỉ để đọc ra toàn khoảng trắng.
        foreach (var row in match.Sheet.RowsUsed(XLCellsUsedOptions.Contents).Where(row => row.RowNumber() > match.HeaderRow))
        {
            var rowNumber = row.RowNumber();
            var cells = new Dictionary<string, string>();
            var any = false;

            foreach (var (key, columnNumber) in match.Found)
            {
                var text = CellText(row.Cell(columnNumber));

                // Ô gộp chỉ giữ giá trị ở ô trên cùng bên trái; các ô còn lại của vùng gộp đọc ra rỗng. Trường thường gộp ô "Lớp",
                // "Giáo viên chủ nhiệm"... cho cả nhóm dòng nên mỗi dòng phải nhận giá trị của vùng gộp chứa nó.
                if (text.Length == 0 && merged.TryGetValue(columnNumber, out var spans))
                {
                    text = SpanText(spans, rowNumber);
                }

                cells[key] = text;
                any |= text.Length > 0;
            }

            if (!any)
            {
                continue;
            }

            if (rows.Count >= maxRows)
            {
                return Result.Failure<SpreadsheetData>(
                    $"File có hơn {maxRows} dòng dữ liệu. Mỗi lần nhập tối đa {maxRows} dòng — hãy tách file thành nhiều lần.");
            }

            rows.Add(new SpreadsheetRow(rowNumber, cells));
        }

        return Result.Success(new SpreadsheetData(rows));
    }

    /// <summary>
    /// Các vùng gộp ô nằm dưới hàng tiêu đề, theo từng cột cần đọc: mỗi vùng là (dòng đầu, dòng cuối, giá trị của ô trên cùng bên trái).
    /// Không trải từng ô ra bộ nhớ nên một vùng gộp kéo dài cả triệu dòng cũng không tốn gì.
    /// </summary>
    private static Dictionary<int, List<(int First, int Last, string Text)>> MergedSpans(HeaderMatch match)
    {
        var spans = new Dictionary<int, List<(int First, int Last, string Text)>>();
        var wanted = match.Found.Values.ToHashSet();

        foreach (var range in match.Sheet.MergedRanges.Take(MaxMergedRanges))
        {
            var first = range.RangeAddress.FirstAddress;
            var last = range.RangeAddress.LastAddress;
            if (last.RowNumber <= match.HeaderRow || last.RowNumber == first.RowNumber)
            {
                continue;
            }

            var text = CellText(match.Sheet.Cell(first.RowNumber, first.ColumnNumber));
            if (text.Length == 0)
            {
                continue;
            }

            for (var column = first.ColumnNumber; column <= last.ColumnNumber; column++)
            {
                if (!wanted.Contains(column))
                {
                    continue;
                }

                if (!spans.TryGetValue(column, out var list))
                {
                    list = [];
                    spans[column] = list;
                }

                list.Add((first.RowNumber, last.RowNumber, text));
            }
        }

        foreach (var list in spans.Values)
        {
            list.Sort((a, b) => a.First.CompareTo(b.First));
        }

        return spans;
    }

    /// <summary>Giá trị của vùng gộp chứa dòng này trong một cột; rỗng nếu dòng không nằm trong vùng gộp nào.</summary>
    private static string SpanText(List<(int First, int Last, string Text)> spans, int rowNumber)
    {
        // Tìm vùng có dòng đầu lớn nhất mà vẫn ≤ dòng đang xét (các vùng cùng một cột không chồng nhau).
        int low = 0, high = spans.Count - 1, hit = -1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (spans[middle].First <= rowNumber)
            {
                hit = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return hit >= 0 && spans[hit].Last >= rowNumber ? spans[hit].Text : string.Empty;
    }

    /// <summary>
    /// Giá trị hiển thị của một ô. Số nguyên in đúng như gõ (mã học sinh 10001 không thành "1E+04"), công thức lấy giá trị đã
    /// lưu sẵn, lỗi công thức coi như ô trống.
    /// </summary>
    private static string CellText(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        var value = cell.HasFormula ? cell.CachedValue : cell.Value;

        return value.Type switch
        {
            XLDataType.Number => FormatNumber(value.GetNumber()),
            XLDataType.Text => Clean(value.GetText()),
            XLDataType.Boolean => value.GetBoolean() ? "TRUE" : "FALSE",
            XLDataType.DateTime or XLDataType.TimeSpan => Clean(cell.GetFormattedString()),
            _ => string.Empty
        };
    }

    private static string FormatNumber(double number)
    {
        if (Math.Abs(number - Math.Round(number)) < 1e-9 && Math.Abs(number) < 1e15)
        {
            return ((long)Math.Round(number)).ToString(CultureInfo.InvariantCulture);
        }

        return number.ToString("0.###############", CultureInfo.InvariantCulture);
    }

    /// <summary>Chuẩn hoá NFC, đổi khoảng trắng không ngắt thành khoảng trắng thường, bỏ ký tự vô hình, cắt hai đầu.</summary>
    private static string Clean(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormC))
        {
            if (character is '​' or '‌' or '‍' or '﻿')
            {
                continue;
            }

            builder.Append(character == ' ' ? ' ' : character);
        }

        return builder.ToString().Trim();
    }

    /// <summary>Khoá so khớp tên cột: chữ thường, bỏ dấu, chỉ giữ chữ và số ("Mã học sinh *" và "ma hoc sinh" là một).</summary>
    private static string Fold(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character == 'đ' || character == 'Đ' ? 'd' : character);
            if (char.IsLetterOrDigit(lower))
            {
                builder.Append(lower);
            }
        }

        return builder.ToString();
    }

    /// <inheritdoc />
    public byte[] BuildTable(SpreadsheetTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add(SafeSheetName(table.SheetName));

        for (var index = 0; index < table.Columns.Count; index++)
        {
            sheet.Column(index + 1).Width = table.Columns[index].Width;
        }

        var row = 1;
        foreach (var line in table.TitleLines)
        {
            var title = sheet.Cell(row, 1);
            SetText(title, line);
            if (row == 1)
            {
                title.Style.Font.Bold = true;
                title.Style.Font.FontSize = 14;
            }

            row++;
        }

        // Một dòng trống ngăn phần tiêu đề với bảng, trừ khi không có tiêu đề nào.
        if (table.TitleLines.Count > 0)
        {
            row++;
        }

        var headerRow = row;
        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index];
            var header = sheet.Cell(headerRow, index + 1);
            SetText(header, column.Header);
            header.Style.Font.Bold = true;
            header.Style.Font.FontColor = XLColor.FromHtml("#3730A3");
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E0E7FF");
            header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            header.Style.Alignment.Horizontal = column.Centered
                ? XLAlignmentHorizontalValues.Center
                : XLAlignmentHorizontalValues.Left;
            header.Style.Alignment.WrapText = true;
            header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        sheet.Row(headerRow).Height = 36;
        row = headerRow + 1;

        foreach (var values in table.Rows)
        {
            WriteRow(sheet, row, table.Columns, values, bold: false);
            row++;
        }

        var firstFooter = row;
        foreach (var values in table.FooterRows)
        {
            WriteRow(sheet, row, table.Columns, values, bold: true);
            row++;
        }

        if (table.FooterRows.Count > 0)
        {
            // Đường kẻ ngăn dữ liệu với các dòng tổng.
            sheet.Range(firstFooter, 1, firstFooter, Math.Max(1, table.Columns.Count)).Style.Border.TopBorder =
                XLBorderStyleValues.Thin;
        }

        sheet.SheetView.FreezeRows(headerRow);
        if (table.FrozenColumns > 0)
        {
            sheet.SheetView.FreezeColumns(table.FrozenColumns);
        }

        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }

    private static void WriteRow(
        IXLWorksheet sheet,
        int row,
        IReadOnlyList<TableColumn> columns,
        IReadOnlyList<object?> values,
        bool bold)
    {
        for (var index = 0; index < columns.Count && index < values.Count; index++)
        {
            var cell = sheet.Cell(row, index + 1);

            switch (values[index])
            {
                case null:
                    break;
                case string text:
                    SetText(cell, text);
                    break;
                case int whole:
                    cell.Value = whole;
                    cell.Style.NumberFormat.Format = "0";
                    break;
                case decimal number:
                    cell.Value = number;
                    cell.Style.NumberFormat.Format = "0.00";
                    break;
                case double number:
                    cell.Value = number;
                    cell.Style.NumberFormat.Format = "0.00";
                    break;
                default:
                    SetText(cell, Convert.ToString(values[index], CultureInfo.InvariantCulture) ?? string.Empty);
                    break;
            }

            cell.Style.Font.Bold = bold;
            cell.Style.Alignment.Horizontal = columns[index].Centered
                ? XLAlignmentHorizontalValues.Center
                : XLAlignmentHorizontalValues.Left;
        }
    }

    /// <summary>
    /// Ghi chuỗi thành ô văn bản. Gán thẳng <see cref="XLCellValue"/> kiểu chữ chứ không qua <c>SetValue</c>: bản này không đoán
    /// kiểu, nên "=1+1" hay "@SUM(A1)" nằm trong ô đúng như đã gõ và Excel không bao giờ tính nó.
    /// </summary>
    private static void SetText(IXLCell cell, string text)
    {
        cell.Value = XLCellValue.FromObject(Clean(text));
    }

    /// <summary>Tên trang tính: Excel cấm <c>\ / ? * [ ] :</c> và giới hạn 31 ký tự.</summary>
    private static string SafeSheetName(string name)
    {
        var cleaned = new string(name.Select(character => "\\/?*[]:".Contains(character) ? ' ' : character).ToArray()).Trim();
        if (cleaned.Length == 0)
        {
            cleaned = "Bảng điểm";
        }

        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }

    /// <inheritdoc />
    public byte[] BuildTemplate(SpreadsheetTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add(template.SheetName);

        for (var index = 0; index < template.Columns.Count; index++)
        {
            var column = template.Columns[index];
            var columnNumber = index + 1;

            // Cột nào cũng định dạng văn bản: mã học sinh "0012" và số điện thoại "0912..." không được Excel nuốt số 0 đầu.
            sheet.Column(columnNumber).Style.NumberFormat.Format = "@";
            sheet.Column(columnNumber).Width = column.Width;

            var header = sheet.Cell(1, columnNumber);
            header.Value = column.Header;
            header.Style.Font.Bold = true;
            header.Style.Font.FontColor = XLColor.FromHtml("#3730A3");
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E0E7FF");
            header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        sheet.Row(1).Height = 24;
        sheet.SheetView.FreezeRows(1);

        // Danh sách chọn sẵn nằm ở một trang riêng (ô tham chiếu được tới trang khác, còn danh sách gõ thẳng bị giới hạn
        // 255 ký tự). Trang này để hiện: người dùng đối chiếu được tên lớp đang có.
        var choiceColumns = template.Columns
            .Select((column, index) => (column, index))
            .Where(item => item.column.Choices is { Count: > 0 })
            .ToList();

        if (choiceColumns.Count > 0)
        {
            var lists = book.Worksheets.Add("Danh sách");
            var listColumn = 1;

            foreach (var (column, index) in choiceColumns)
            {
                lists.Cell(1, listColumn).Value = column.Header.TrimEnd('*', ' ');
                lists.Cell(1, listColumn).Style.Font.Bold = true;
                lists.Column(listColumn).Width = Math.Max(18, column.Width);

                var values = column.Choices!;
                for (var row = 0; row < values.Count; row++)
                {
                    lists.Cell(row + 2, listColumn).Value = values[row];
                }

                var range = lists.Range(2, listColumn, values.Count + 1, listColumn);
                var validation = sheet.Range(2, index + 1, template.MaxRows + 1, index + 1).CreateDataValidation();
                validation.List(range, true);
                validation.IgnoreBlanks = true;

                // Chỉ gợi ý: tên không có trong danh sách vẫn nhập được, và khi nhập vào hệ thống sẽ được báo lỗi đúng dòng.
                validation.ShowErrorMessage = false;
                listColumn++;
            }
        }

        var guide = book.Worksheets.Add("Hướng dẫn");
        guide.Column(1).Width = 110;
        for (var row = 0; row < template.GuideLines.Count; row++)
        {
            var cell = guide.Cell(row + 1, 1);
            cell.Value = template.GuideLines[row];
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            if (row == 0)
            {
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 14;
            }
        }

        sheet.SetTabActive();

        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }
}
