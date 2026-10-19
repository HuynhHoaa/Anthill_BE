using System.Globalization;
using System.Text;

namespace Anthill.Application.Common;

/// <summary>
/// Chuẩn hoá chuỗi đọc từ file Excel trước khi so khớp và ghi: cùng một phép ở mọi nơi nhập liệu, để "Lớp 10A1" gõ hai kiểu hay
/// tên có hai dấu cách đều ra cùng một giá trị.
/// </summary>
public static class ImportText
{
    /// <summary>Gộp mọi dãy khoảng trắng thành một dấu cách, cắt hai đầu, chuẩn hoá NFC.</summary>
    public static string Collapse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;

        foreach (var character in value.Trim().Normalize(NormalizationForm.FormC))
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                }

                previousWasSpace = true;
                continue;
            }

            previousWasSpace = false;
            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Khoá so sánh không phân biệt hoa thường và dấu: "Nguyễn Văn An" và "nguyen van an" là một. Dùng để khớp tên lớp, tên
    /// môn và để nhận ra cùng một người ở hai nơi.
    /// </summary>
    public static string Fold(string? value)
    {
        var collapsed = Collapse(value);
        var builder = new StringBuilder(collapsed.Length);

        foreach (var character in collapsed.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Số điện thoại từ Excel: ô kiểu số làm mất số 0 đầu ("0912345678" thành 912345678), nên chuỗi toàn chữ số dài chín ký tự được
    /// thêm lại số 0. Trả chuỗi rỗng khi ô trống.
    /// </summary>
    public static string Phone(string? value)
    {
        var collapsed = Collapse(value);

        return collapsed.Length == 9 && collapsed.All(char.IsAsciiDigit) ? "0" + collapsed : collapsed;
    }
}
