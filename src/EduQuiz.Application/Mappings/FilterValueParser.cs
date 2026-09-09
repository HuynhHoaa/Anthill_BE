namespace EduQuiz.Application.Mappings;

/// <summary>
/// Đọc các ô lọc mà frontend gửi lên dưới dạng chuỗi — mức độ, trạng thái, vòng đời, chủ đề.
/// </summary>
/// <remarks>
/// <para>
/// Tồn tại vì logic này từng nằm hai chỗ. Bản sao thứ hai được viết lại từ đầu cho bộ lọc vòng đời
/// và <b>quên mất <c>Enum.IsDefined</c></b>, khiến <c>?lifecycle=42</c> đi lọt xuống tận repository.
/// Hai bản sao của cùng một quy tắc thì sớm muộn cũng lệch nhau; gộp về một chỗ là cách duy nhất
/// chắc chắn không lệch nữa.
/// </para>
/// <para>
/// Quy ước chung của mọi ô lọc: <c>"all"</c> và chuỗi rỗng nghĩa là <b>không lọc</b>, còn giá trị
/// lạ thì <b>từ chối</b> chứ không âm thầm bỏ lọc — trả về nhiều hơn người dùng yêu cầu mà vẫn kèm
/// mã 200 là kiểu sai không ai phát hiện.
/// </para>
/// </remarks>
public static class FilterValueParser
{
    public const string AllFilterValue = "all";

    /// <summary>Có phải giá trị "không lọc" hay không: bỏ trống, hoặc đúng chữ <c>"all"</c>.</summary>
    public static bool IsAll(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || string.Equals(value.Trim(), AllFilterValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Đọc một giá trị enum bắt buộc.
    /// </summary>
    /// <remarks>
    /// <c>Enum.IsDefined</c> là phần không được bỏ. <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>
    /// nhận cả chuỗi số và trả về true cho một giá trị chưa từng được khai báo: client gửi
    /// <c>"7"</c> thì parse thành <c>(TEnum)7</c> rồi đi thẳng xuống database, nơi nó không khớp
    /// dòng nào và sinh ra một kết quả rỗng trông như thật.
    /// </remarks>
    public static bool TryParseEnum<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out result) && Enum.IsDefined(result);
    }

    /// <summary>Đọc một ô lọc enum. Trả NULL cho <c>"all"</c>/bỏ trống, false cho giá trị lạ.</summary>
    public static bool TryParseFilter<TEnum>(string? value, out TEnum? result) where TEnum : struct, Enum
    {
        result = null;

        if (IsAll(value))
        {
            return true;
        }

        if (!TryParseEnum<TEnum>(value, out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    /// <summary>Ô lọc dạng chữ, ví dụ chủ đề: <c>"all"</c> và chuỗi rỗng thành NULL, còn lại trả chuỗi đã trim.</summary>
    public static string? NormalizeTextFilter(string? value) => IsAll(value) ? null : value!.Trim();
}
