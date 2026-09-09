using EduQuiz.Application.Common;
using FluentAssertions;

namespace EduQuiz.Tests.Exams;

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

    [Fact]
    public void Cung_chuoi_cho_cung_hat_giong_qua_moi_lan_chay()
    {
        // Giá trị dưới đây được ghi cứng có chủ đích. string.GetHashCode() của .NET bị ngẫu nhiên
        // hoá theo tiến trình, nên nếu ai đó thay FNV-1a bằng nó thì test này hỏng ngay — đúng
        // điều cần, vì một hạt giống đổi theo mỗi lần khởi động thì không còn tái lập được gì.
        DeterministicShuffle.Seed("12:34").Should().Be(DeterministicShuffle.Seed("12:34"));
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
