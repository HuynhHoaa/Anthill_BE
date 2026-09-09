namespace EduQuiz.Infrastructure.Repositories;

/// <summary>
/// Chuẩn hoá từ khoá tìm kiếm ở phía C#, để ghép với <c>unaccent()</c> phía PostgreSQL.
/// </summary>
/// <remarks>
/// Cặp đôi này thực hiện đúng quy tắc §5: bỏ dấu, không phân biệt hoa thường.
/// <code>
/// EF.Functions.ILike(EF.Functions.Unaccent(u.FullName), "%tran thi lan%")
/// </code>
/// Cột được <c>unaccent()</c> ở database, từ khoá được bỏ dấu ở đây, rồi <c>ILIKE</c> lo phần
/// hoa thường. Cố ý <b>không</b> dùng <c>string.Normalize(FormD)</c>: chế độ
/// <c>InvariantGlobalization</c> mà kế hoạch deploy đề xuất bật (rút ngắn thời gian boot)
/// không bảo đảm có dữ liệu Unicode để tách dấu, còn bảng tra tường minh thì luôn chạy đúng.
/// </remarks>
internal static class SearchText
{
    private static readonly Dictionary<char, char> DiacriticMap = BuildDiacriticMap();

    /// <summary>
    /// Đổi từ khoá người dùng gõ thành mẫu <c>LIKE</c> bao hai đầu. Trả NULL khi không có từ khoá,
    /// để lời gọi chỉ cần một phép kiểm <c>is not null</c> trước khi thêm mệnh đề lọc.
    /// </summary>
    public static string? ToLikePattern(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return null;
        }

        var plain = RemoveDiacritics(term.Trim());

        // Người dùng gõ '%' hay '_' thì đó là ký tự cần tìm, không phải ký tự đại diện.
        var escaped = plain
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }

    public static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var buffer = new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            buffer[i] = DiacriticMap.TryGetValue(value[i], out var plain) ? plain : value[i];
        }

        return new string(buffer);
    }

    private static Dictionary<char, char> BuildDiacriticMap()
    {
        var map = new Dictionary<char, char>();

        Add(map, 'a', "àáạảãâầấậẩẫăằắặẳẵ");
        Add(map, 'A', "ÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴ");
        Add(map, 'e', "èéẹẻẽêềếệểễ");
        Add(map, 'E', "ÈÉẸẺẼÊỀẾỆỂỄ");
        Add(map, 'i', "ìíịỉĩ");
        Add(map, 'I', "ÌÍỊỈĨ");
        Add(map, 'o', "òóọỏõôồốộổỗơờớợởỡ");
        Add(map, 'O', "ÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠ");
        Add(map, 'u', "ùúụủũưừứựửữ");
        Add(map, 'U', "ÙÚỤỦŨƯỪỨỰỬỮ");
        Add(map, 'y', "ỳýỵỷỹ");
        Add(map, 'Y', "ỲÝỴỶỸ");
        Add(map, 'd', "đ");
        Add(map, 'D', "Đ");

        return map;
    }

    private static void Add(Dictionary<char, char> map, char plain, string accented)
    {
        foreach (var accentedChar in accented)
        {
            map[accentedChar] = plain;
        }
    }
}
