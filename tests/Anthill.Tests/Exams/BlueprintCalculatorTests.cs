using Anthill.Application.Common;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Exams;

/// <summary>
/// Phép chia số câu theo tỷ lệ độ khó phải khớp <b>từng đơn vị</b> với
/// <c>calculateRequiredCounts</c> của frontend (§5).
/// </summary>
/// <remarks>
/// Đây là chỗ đáng viết test nhất của bước 4, vì nó hội đủ ba điều kiện: là phép tính thuần nên
/// kiểm chứng được bằng một bảng số; sai thì <b>không</b> ném lỗi mà chỉ lệch một câu, nên chạy
/// tay rất khó phát hiện; và hậu quả thì thấy ngay — giáo viên xem trước thấy 12 câu Dễ, đề sinh
/// ra 11 câu.
///
/// Giá trị mong đợi ở mỗi ca đều được tính bằng đúng công thức của frontend rồi ghi vào đây, chứ
/// không lấy từ kết quả chạy của chính hàm đang kiểm.
/// </remarks>
public class BlueprintCalculatorTests
{
    private static (int Easy, int Medium, int Hard) Split(int total, int easy, int medium) =>
        Unpack(BlueprintCalculator.CalculateRequiredCounts(total, easy, medium));

    private static (int Easy, int Medium, int Hard) Unpack(IReadOnlyDictionary<QuestionDifficulty, int> counts) =>
        (counts[QuestionDifficulty.Easy], counts[QuestionDifficulty.Medium], counts[QuestionDifficulty.Hard]);

    [Theory]
    // Tỷ lệ chia chẵn — không có gì để làm tròn.
    [InlineData(30, 40, 40, 12, 12, 6)]
    [InlineData(20, 50, 25, 10, 5, 5)]
    [InlineData(10, 100, 0, 10, 0, 0)]
    // Tỷ lệ lẻ: mức Khó nhận phần dư nên tổng vẫn đúng bằng total.
    // 33% của 10 là 3.3 → 3 cho cả Dễ lẫn Trung bình, Khó lấy nốt 4.
    [InlineData(10, 33, 33, 3, 3, 4)]
    // 1/3 của 7 là 2.333… → 2 và 2, Khó lấy 3.
    [InlineData(7, 33, 33, 2, 2, 3)]
    public void Chia_dung_ty_le_va_tong_luon_bang_total(
        int total,
        int easyPercentage,
        int mediumPercentage,
        int expectedEasy,
        int expectedMedium,
        int expectedHard)
    {
        var (easy, medium, hard) = Split(total, easyPercentage, mediumPercentage);

        easy.Should().Be(expectedEasy);
        medium.Should().Be(expectedMedium);
        hard.Should().Be(expectedHard);
        (easy + medium + hard).Should().Be(total, "mức Khó nhận phần dư nên tổng ba mức luôn khít");
    }

    /// <summary>
    /// Ca quan trọng nhất của cả file: những mốc <c>.5</c> mà .NET và JavaScript làm tròn khác nhau.
    /// </summary>
    /// <remarks>
    /// <c>Math.Round</c> mặc định của .NET làm tròn về số <b>chẵn</b> gần nhất, nên <c>2.5 → 2</c>
    /// và <c>0.5 → 0</c>. <c>Math.round</c> của JavaScript làm tròn lên, cho <c>3</c> và <c>1</c>.
    /// Nếu <see cref="BlueprintCalculator"/> quên <see cref="MidpointRounding.AwayFromZero"/> thì
    /// đúng những ca dưới đây sai, còn mọi ca khác vẫn đúng — nên không có test này thì lỗi lọt.
    /// </remarks>
    [Theory]
    // 5% của 50 = 2.5. Làm tròn chẵn cho 2, JavaScript cho 3.
    [InlineData(50, 5, 5, 3, 3, 44)]
    // 5% của 10 = 0.5. Làm tròn chẵn cho 0, JavaScript cho 1.
    [InlineData(10, 5, 5, 1, 1, 8)]
    // 25% của 10 = 2.5 ở cả hai mức đầu.
    [InlineData(10, 25, 25, 3, 3, 4)]
    public void Lam_tron_moc_nua_don_vi_giong_JavaScript_khong_giong_mac_dinh_cua_dotnet(
        int total,
        int easyPercentage,
        int mediumPercentage,
        int expectedEasy,
        int expectedMedium,
        int expectedHard)
    {
        var (easy, medium, hard) = Split(total, easyPercentage, mediumPercentage);

        easy.Should().Be(expectedEasy);
        medium.Should().Be(expectedMedium);
        hard.Should().Be(expectedHard);
    }

    /// <summary>Giá trị vô lý bị kẹp thay vì làm hỏng phép chia.</summary>
    /// <remarks>
    /// Validator đã chặn tỷ lệ ngoài 0..100 và số câu dưới 1 trước khi tới đây, nên những ca này
    /// mô tả <i>hàng rào cuối</i>: hàm vẫn phải trả về một bộ ba hợp lệ nếu có ai gọi thẳng nó.
    /// Điều bắt buộc trong mọi trường hợp là ba số không âm và cộng lại đúng bằng total.
    /// </remarks>
    [Theory]
    [InlineData(10, 200, 0)]   // tỷ lệ vượt 100 — Dễ bị kẹp về đúng total
    [InlineData(10, -50, 50)]  // tỷ lệ âm — coi như 0
    [InlineData(10, 80, 80)]   // tổng vượt 100 — Trung bình bị kẹp bởi phần còn lại
    [InlineData(0, 40, 40)]    // không có câu nào
    public void Gia_tri_vo_ly_bi_kep_chu_khong_lam_hong_phep_chia(
        int total,
        int easyPercentage,
        int mediumPercentage)
    {
        var (easy, medium, hard) = Split(total, easyPercentage, mediumPercentage);

        easy.Should().BeGreaterThanOrEqualTo(0);
        medium.Should().BeGreaterThanOrEqualTo(0);
        hard.Should().BeGreaterThanOrEqualTo(0);
        (easy + medium + hard).Should().Be(Math.Max(0, total));
    }

    [Fact]
    public void Tong_ba_muc_luon_bang_total_voi_moi_to_hop_ty_le_hop_le()
    {
        // Quét toàn bộ tổ hợp tỷ lệ cộng đúng 100 trên nhiều cỡ đề. Một vòng lặp ở đây đáng giá
        // hơn hai chục ca InlineData: bất biến cần bảo vệ là "tổng luôn khít", và nó phải đúng
        // với mọi tỷ lệ chứ không chỉ vài tỷ lệ đẹp.
        int[] totals = [1, 5, 7, 10, 13, 20, 30, 45, 100];

        foreach (var total in totals)
        {
            for (var easyPercentage = 0; easyPercentage <= 100; easyPercentage++)
            {
                for (var mediumPercentage = 0; mediumPercentage <= 100 - easyPercentage; mediumPercentage++)
                {
                    var (easy, medium, hard) = Split(total, easyPercentage, mediumPercentage);

                    (easy + medium + hard).Should().Be(
                        total,
                        "tỷ lệ {0}/{1} trên đề {2} câu vẫn phải chia khít",
                        easyPercentage,
                        mediumPercentage,
                        total);
                }
            }
        }
    }
}
