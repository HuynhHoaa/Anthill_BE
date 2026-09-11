namespace Anthill.Application.Common;

/// <summary>
/// Xáo trộn Fisher-Yates với hạt giống cố định — dùng khi sinh đề (§5).
/// </summary>
/// <remarks>
/// <para>
/// Hai thứ ở đây cố ý <b>không</b> dùng thư viện chuẩn, và lý do giống nhau: cả hai đều không cam
/// kết cho ra cùng một kết quả ở hai lần chạy khác nhau.
/// </para>
/// <list type="number">
/// <item><b><c>string.GetHashCode()</c> bị ngẫu nhiên hoá theo tiến trình.</b> .NET trộn một giá
/// trị ngẫu nhiên của phiên vào hàm băm chuỗi để chống tấn công va chạm hash. Nghĩa là cùng một
/// chuỗi <c>"12:34"</c> cho ra hai con số khác nhau ở hai lần khởi động API — hạt giống dựng từ
/// nó thì "cố định" chỉ trong vòng một tiến trình. <see cref="Seed"/> dùng FNV-1a, một hàm băm
/// khai báo tường minh nên không đổi theo phiên bản runtime hay theo lần chạy.</item>
/// <item><b><see cref="Random"/> có hạt giống không được bảo đảm ổn định giữa các phiên bản .NET.</b>
/// Tài liệu của Microsoft nói rõ thuật toán có thể đổi. Nâng .NET lên một phiên bản mà đề sinh ra
/// đổi thứ tự là chuyện rất khó truy ra nguyên nhân, nên nguồn ngẫu nhiên ở đây được viết thẳng
/// bằng xorshift — mười dòng, và đọc là biết nó làm gì.</item>
/// </list>
/// <para>
/// Cần nói rõ mức độ quan trọng: bản đề <b>được lưu xuống database</b> ngay khi sinh, nên thứ giữ
/// cho học sinh F5 vẫn thấy đúng bộ câu cũ là bảng <c>ExamPaperQuestion</c> chứ không phải hạt
/// giống. Tính tái lập ở đây phục vụ việc khác: dựng lại đúng một đề đã sinh khi cần đối chiếu,
/// và làm cho hành vi sinh đề kiểm chứng được bằng test thay vì phải chấp nhận "chạy ra sao thì ra".
/// </para>
/// </remarks>
public static class DeterministicShuffle
{
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;

    /// <summary>
    /// Hạt giống ổn định dựng từ một chuỗi, bằng FNV-1a.
    /// </summary>
    /// <remarks>
    /// Trả về giá trị khác 0 trong mọi trường hợp: xorshift đứng yên tại 0, nên một hạt giống 0
    /// sẽ cho ra dãy toàn số không và mọi đề đều giữ nguyên thứ tự gốc.
    /// </remarks>
    public static uint Seed(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var hash = FnvOffsetBasis;

        foreach (var symbol in value)
        {
            hash ^= symbol;
            hash *= FnvPrime;
        }

        return hash == 0 ? FnvPrime : hash;
    }

    /// <summary>
    /// Trả về một <b>bản sao</b> đã xáo trộn. Không sửa danh sách gốc, vì cùng một tập ứng viên
    /// còn được dùng lại cho học sinh tiếp theo khi bật <c>randomizePerStudent</c>.
    /// </summary>
    public static List<T> Shuffle<T>(IReadOnlyList<T> source, uint seed)
    {
        ArgumentNullException.ThrowIfNull(source);

        var items = source.ToList();
        var random = new XorShift(seed);

        // Fisher-Yates: duyệt từ cuối về đầu, mỗi bước đổi chỗ phần tử hiện tại với một phần tử
        // bất kỳ trong đoạn chưa xử lý. Mỗi hoán vị có xác suất bằng nhau — khác hẳn kiểu sắp xếp
        // theo khoá ngẫu nhiên, vốn lệch phân phối và còn phụ thuộc thuật toán sắp xếp.
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapWith = (int)(random.Next() % (uint)(index + 1));
            (items[index], items[swapWith]) = (items[swapWith], items[index]);
        }

        return items;
    }

    /// <summary>
    /// Xorshift32 của George Marsaglia. Chất lượng thống kê vừa đủ cho việc trộn thứ tự câu hỏi,
    /// và <b>không</b> dành cho mục đích mật mã — ở đây không cần: bộ câu là bí mật vì học sinh
    /// không đọc được bảng, không phải vì thứ tự khó đoán.
    /// </summary>
    private sealed class XorShift
    {
        private uint _state;

        public XorShift(uint seed) => _state = seed;

        public uint Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }
    }
}
