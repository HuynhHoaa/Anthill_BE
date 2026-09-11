using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>
/// Chia tổng số câu của một đề thành ba mức độ theo tỷ lệ phần trăm.
/// </summary>
/// <remarks>
/// <para>
/// Tách khỏi service vì đây là phép tính thuần — không chạm database, không cần danh tính — nên
/// kiểm chứng được bằng một bảng số. Nó phải khớp <b>từng đơn vị</b> với <c>calculateRequiredCounts</c>
/// của frontend (§5): lệch một câu là giáo viên xem trước thấy một đằng, đề sinh ra một nẻo.
/// </para>
/// <para>Hai chi tiết dễ làm sai:</para>
/// <list type="number">
/// <item><b><see cref="MidpointRounding.AwayFromZero"/>, không phải mặc định của .NET.</b>
/// <c>Math.Round</c> mặc định làm tròn về số chẵn, nên <c>2.5 → 2</c>; <c>Math.round</c> của
/// JavaScript cho <c>3</c>. Đúng ở mốc <c>.5</c> hai bên lệch nhau một câu.</item>
/// <item><b>Mức Khó nhận phần dư</b> thay vì cũng được làm tròn. Nhờ vậy tổng ba mức luôn đúng
/// bằng <c>totalQuestions</c> kể cả khi ba tỷ lệ lẻ — nếu làm tròn cả ba thì 33/33/34 trên 10 câu
/// ra 3+3+3 = 9, thiếu một câu mà không ai báo.</item>
/// </list>
/// </remarks>
public static class BlueprintCalculator
{
    /// <summary>
    /// Số câu cần lấy cho từng mức độ.
    /// </summary>
    /// <remarks>
    /// Cố ý <b>không</b> nhận tỷ lệ mức Khó: mức đó luôn là phần còn lại, nhận thêm tham số chỉ
    /// tạo cơ hội cho người gọi truyền vào một con số rồi tưởng nó có tác dụng. Việc kiểm tổng ba
    /// tỷ lệ có bằng 100 hay không thuộc về validator, không thuộc về phép chia này.
    /// </remarks>
    public static IReadOnlyDictionary<QuestionDifficulty, int> CalculateRequiredCounts(
        int totalQuestions,
        int easyPercentage,
        int mediumPercentage)
    {
        var total = Math.Max(0, totalQuestions);

        var easy = Math.Clamp(ScaleAndRound(total, easyPercentage), 0, total);
        var medium = Math.Min(total - easy, Math.Max(0, ScaleAndRound(total, mediumPercentage)));
        var hard = Math.Max(0, total - easy - medium);

        return new Dictionary<QuestionDifficulty, int>
        {
            [QuestionDifficulty.Easy] = easy,
            [QuestionDifficulty.Medium] = medium,
            [QuestionDifficulty.Hard] = hard
        };
    }

    /// <summary>
    /// <c>round(total * percentage / 100)</c> theo đúng ngữ nghĩa <c>Math.round</c> của JavaScript.
    /// </summary>
    /// <remarks>
    /// <c>Math.round</c> của JavaScript làm tròn nửa về phía <b>dương vô cực</b>, còn
    /// <see cref="MidpointRounding.AwayFromZero"/> làm tròn nửa ra <b>xa số không</b>. Hai định
    /// nghĩa này chỉ khác nhau ở số âm, mà tỷ lệ phần trăm đã bị kẹp về không âm trước khi tới
    /// đây, nên trong phạm vi bài toán chúng trùng nhau.
    /// </remarks>
    private static int ScaleAndRound(int total, int percentage)
    {
        var scaled = total * (double)Math.Max(0, percentage) / 100d;
        return (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
    }
}
