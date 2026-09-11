using Anthill.Application.Common;
using FluentAssertions;

namespace Anthill.Tests.Exams;

/// <summary>
/// Nguồn ngẫu nhiên dùng để sinh đề. Điều phải bảo đảm: <b>cùng hạt giống thì cùng kết quả</b>,
/// và hai hạt giống khác nhau thì gần như luôn khác kết quả.
/// </summary>
public class DeterministicShuffleTests
{
    private static readonly int[] Source = Enumerable.Range(1, 30).ToArray();

    [Fact]
    public void Cung_hat_giong_cho_cung_thu_tu()
    {
        var first = DeterministicShuffle.Shuffle(Source, 12345);
        var second = DeterministicShuffle.Shuffle(Source, 12345);

        second.Should().Equal(first);
    }

    [Fact]
    public void Hat_giong_khac_cho_thu_tu_khac()
    {
        var first = DeterministicShuffle.Shuffle(Source, 1);
        var second = DeterministicShuffle.Shuffle(Source, 2);

        second.Should().NotEqual(first);
    }

    [Fact]
    public void Xao_tron_giu_nguyen_tap_phan_tu_khong_mat_khong_lap()
    {
        // Một lỗi Fisher-Yates cài sai kinh điển là nhân bản hoặc đánh rơi phần tử. Với đề thi thì
        // đó là một câu hỏi xuất hiện hai lần, hoặc một câu biến mất mà tổng điểm vẫn tính đủ.
        var shuffled = DeterministicShuffle.Shuffle(Source, 999);

        shuffled.Should().HaveCount(Source.Length);
        shuffled.Should().OnlyHaveUniqueItems();
        shuffled.Should().BeEquivalentTo(Source);
    }

    [Fact]
    public void Khong_sua_danh_sach_goc()
    {
        // Tập ứng viên được dùng lại cho học sinh tiếp theo khi bật randomizePerStudent. Xáo tại
        // chỗ thì em thứ hai nhận một tập đã bị xáo sẵn, và hạt giống không còn quyết định gì nữa.
        var original = new[] { 1, 2, 3, 4, 5 };
        var copy = original.ToArray();

        DeterministicShuffle.Shuffle(original, 42);

        original.Should().Equal(copy);
    }

    [Fact]
    public void Hat_giong_dung_ngay_ca_khi_chuoi_bam_ra_khong()
    {
        // Xorshift đứng yên tại 0: một hạt giống 0 cho ra dãy toàn số không, và mọi đề sẽ giữ
        // nguyên thứ tự gốc. Seed() phải tự tránh giá trị đó.
        var seeds = Enumerable.Range(0, 5000).Select(i => DeterministicShuffle.Seed($"exam:{i}"));

        seeds.Should().NotContain(0u);
    }

    /// <summary>
    /// Ghim <b>giá trị cụ thể</b> của hàm băm, không chỉ ghim tính nhất quán trong một lần chạy.
    /// </summary>
    /// <remarks>
    /// So một lời gọi với chính nó thì <c>string.GetHashCode()</c> cũng qua được, vì trong cùng một
    /// tiến trình nó vẫn ổn định — mà đó chính là thứ phải chặn: hàm băm ấy được ngẫu nhiên hoá
    /// theo <i>từng lần khởi động</i>, nên nó hỏng ở đúng chỗ test không nhìn thấy.
    ///
    /// Ba số dưới đây được tính bằng một bản cài đặt FNV-1a 32-bit độc lập, không lấy từ kết quả
    /// chạy của chính hàm đang kiểm. Đổi cách băm là ba dòng này hỏng ngay.
    /// </remarks>
    [Theory]
    [InlineData("12:34", 1879403703u)]
    [InlineData("7", 839689206u)]
    [InlineData("exam:1:answers:5", 1804204935u)]
    public void Ham_bam_cho_dung_gia_tri_da_ghim(string input, uint expected)
    {
        DeterministicShuffle.Seed(input).Should().Be(expected);
    }

    [Fact]
    public void Chuoi_khac_nhau_cho_hat_giong_khac_nhau()
    {
        DeterministicShuffle.Seed("12:34").Should().NotBe(DeterministicShuffle.Seed("12:35"));
    }

    [Fact]
    public void Moi_phan_tu_deu_co_co_hoi_toi_moi_vi_tri()
    {
        // Fisher-Yates đúng thì phân phối đều. Không kiểm chặt bằng thống kê ở đây — chỉ cần thấy
        // phần tử đầu tiên không bị dính cứng một chỗ, đủ để phát hiện kiểu cài sai phổ biến là
        // lặp từ đầu về cuối với chỉ số ngẫu nhiên trên toàn dải.
        var positionsOfOne = new HashSet<int>();

        for (var seed = 1u; seed <= 200; seed++)
        {
            positionsOfOne.Add(DeterministicShuffle.Shuffle(Source, seed).IndexOf(1));
        }

        positionsOfOne.Should().HaveCountGreaterThan(10);
    }
}
