namespace Anthill.Application.Common;

/// <summary>Mã đề hiện cho giáo viên: đề 0 là "A", đề 1 là "B"… (quá 26 thì dùng số thứ tự).</summary>
public static class PaperLabels
{
    public static string Of(int variantIndex) =>
        variantIndex is >= 0 and < 26 ? ((char)('A' + variantIndex)).ToString() : (variantIndex + 1).ToString();
}
