using Anthill.Application.Common;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Exams;

public class PaperPartitionerTests
{
    // Mã câu hỏi theo mức: Dễ 1.., Trung bình 1001.., Khó 2001.. — nhìn mã là biết mức.
    private static List<int> Pool(QuestionDifficulty level, int size)
    {
        var start = level switch
        {
            QuestionDifficulty.Easy => 1,
            QuestionDifficulty.Medium => 1001,
            _ => 2001
        };

        return Enumerable.Range(start, size).ToList();
    }

    private static QuestionDifficulty LevelOf(int questionId) =>
        questionId < 1001 ? QuestionDifficulty.Easy : questionId < 2001 ? QuestionDifficulty.Medium : QuestionDifficulty.Hard;

    private static IReadOnlyList<IReadOnlyList<int>> Split(
        int easy, int medium, int hard, int papers, int easyEach, int mediumEach, int hardEach, int seed = 7) =>
        PaperPartitioner.Partition(
            Pool(QuestionDifficulty.Easy, easy),
            Pool(QuestionDifficulty.Medium, medium),
            Pool(QuestionDifficulty.Hard, hard),
            papers,
            easyEach,
            mediumEach,
            hardEach,
            new Random(seed));

    private static Dictionary<int, int> Usage(IEnumerable<IReadOnlyList<int>> papers) =>
        papers.SelectMany(paper => paper).GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());

    [Fact]
    public void Du_cau_thi_chia_4_de_khong_trung_cau_nao()
    {
        // 120 câu cân bằng 40/40/40 chia 4 đề × 30 câu (10/10/10).
        var papers = Split(40, 40, 40, papers: 4, easyEach: 10, mediumEach: 10, hardEach: 10);

        papers.Should().HaveCount(4);
        papers.Should().AllSatisfy(paper =>
        {
            paper.Should().HaveCount(30);
            paper.Count(id => LevelOf(id) == QuestionDifficulty.Easy).Should().Be(10);
            paper.Count(id => LevelOf(id) == QuestionDifficulty.Medium).Should().Be(10);
            paper.Count(id => LevelOf(id) == QuestionDifficulty.Hard).Should().Be(10);
        });

        // Dùng hết 120 câu, mỗi câu đúng một lần.
        var usage = Usage(papers);
        usage.Should().HaveCount(120);
        usage.Values.Should().AllBeEquivalentTo(1);
    }

    [Fact]
    public void De_lon_110_cau_chia_4_de_x_30_cau_van_du_30_cau_va_dung_ty_le_nhung_co_cau_trung()
    {
        // Ví dụ của chủ sản phẩm: 110 câu (37/37/36), mỗi đề phải đủ 30 câu 10/10/10 dù tổng cần 120 > 110.
        var papers = Split(37, 37, 36, papers: 4, easyEach: 10, mediumEach: 10, hardEach: 10);

        papers.Should().AllSatisfy(paper =>
        {
            paper.Should().HaveCount(30);
            paper.Distinct().Should().HaveCount(30, "một đề không có hai câu giống nhau");
            paper.Count(id => LevelOf(id) == QuestionDifficulty.Easy).Should().Be(10);
            paper.Count(id => LevelOf(id) == QuestionDifficulty.Medium).Should().Be(10);
            paper.Count(id => LevelOf(id) == QuestionDifficulty.Hard).Should().Be(10);
        });

        // Thiếu 3 / 3 / 4 câu nên đúng chừng đó câu mỗi mức nằm ở hai đề; còn lại mỗi câu một đề, không câu nào bị bỏ.
        var usage = Usage(papers);
        usage.Should().HaveCount(110);
        usage.Where(pair => LevelOf(pair.Key) == QuestionDifficulty.Easy).Count(pair => pair.Value == 2).Should().Be(3);
        usage.Where(pair => LevelOf(pair.Key) == QuestionDifficulty.Medium).Count(pair => pair.Value == 2).Should().Be(3);
        usage.Where(pair => LevelOf(pair.Key) == QuestionDifficulty.Hard).Count(pair => pair.Value == 2).Should().Be(4);
        usage.Values.Should().OnlyContain(count => count == 1 || count == 2);
    }

    [Fact]
    public void De_lon_110_cau_chia_4_de_x_20_cau_thi_khong_phai_trung_va_con_du_cau()
    {
        // 20 câu theo 33/33/34 → 7/7/6 mỗi đề; 4 đề cần 28/28/24 ≤ 37/37/36 nên chia riêng được hoàn toàn.
        var papers = Split(37, 37, 36, papers: 4, easyEach: 7, mediumEach: 7, hardEach: 6);

        papers.Should().AllSatisfy(paper => paper.Should().HaveCount(20));
        Usage(papers).Values.Should().AllBeEquivalentTo(1);
        Usage(papers).Should().HaveCount(28 + 28 + 24);
    }

    [Fact]
    public void Muc_khong_can_cau_nao_thi_bo_qua()
    {
        var papers = Split(10, 0, 10, papers: 2, easyEach: 5, mediumEach: 0, hardEach: 5);

        papers.Should().AllSatisfy(paper => paper.Count(id => LevelOf(id) == QuestionDifficulty.Medium).Should().Be(0));
        papers.Should().AllSatisfy(paper => paper.Should().HaveCount(10));
    }

    [Fact]
    public void Mot_de_thi_bang_dung_so_cau_can()
    {
        var papers = Split(5, 5, 5, papers: 1, easyEach: 5, mediumEach: 5, hardEach: 5);

        papers.Should().ContainSingle();
        papers[0].Should().HaveCount(15);
        papers[0].Distinct().Should().HaveCount(15);
    }

    [Fact]
    public void Thieu_cau_thi_bao_loi_chu_khong_chia_bua()
    {
        var act = () => Split(8, 40, 40, papers: 4, easyEach: 10, mediumEach: 10, hardEach: 10);

        act.Should().Throw<ArgumentException>().WithMessage("*10 câu*8 câu*");
    }

    [Fact]
    public void Cung_hat_giong_thi_cung_ket_qua_con_khac_hat_giong_thi_khac()
    {
        var first = Split(37, 37, 36, 4, 10, 10, 10, seed: 1);
        var again = Split(37, 37, 36, 4, 10, 10, 10, seed: 1);
        var other = Split(37, 37, 36, 4, 10, 10, 10, seed: 2);

        first.Select(paper => paper.ToList()).Should().BeEquivalentTo(again.Select(paper => paper.ToList()),
            options => options.WithStrictOrdering());
        first.SelectMany(paper => paper).Should().NotEqual(other.SelectMany(paper => paper));
    }

    [Fact]
    public void Tinh_chat_giu_nguyen_tren_nhieu_cau_hinh_ngau_nhien()
    {
        // Thử 400 cấu hình bất kỳ: đúng số câu, đúng số câu từng mức, không trùng trong một đề, các câu được dùng
        // đều tay (chênh tối đa một lần), và đúng với con số PaperPlanner tính trước.
        var meta = new Random(2026);

        for (var round = 0; round < 400; round++)
        {
            var papersCount = meta.Next(1, PaperPlanner.MaxPaperCount + 1);
            var pools = new[] { meta.Next(1, 61), meta.Next(1, 61), meta.Next(1, 61) };
            var needs = pools.Select(size => meta.Next(0, size + 1)).ToArray();

            var papers = Split(pools[0], pools[1], pools[2], papersCount, needs[0], needs[1], needs[2], seed: round);

            var plan = PaperPlanner.Evaluate(
                new Dictionary<QuestionDifficulty, int>
                {
                    [QuestionDifficulty.Easy] = pools[0],
                    [QuestionDifficulty.Medium] = pools[1],
                    [QuestionDifficulty.Hard] = pools[2]
                },
                new Dictionary<QuestionDifficulty, int>
                {
                    [QuestionDifficulty.Easy] = needs[0],
                    [QuestionDifficulty.Medium] = needs[1],
                    [QuestionDifficulty.Hard] = needs[2]
                },
                papersCount);

            plan.Feasible.Should().BeTrue();
            papers.Should().HaveCount(papersCount);

            var usage = Usage(papers);

            foreach (var (levelPlan, index) in plan.Levels.Select((level, i) => (level, i)))
            {
                var level = levelPlan.Level;

                foreach (var paper in papers)
                {
                    paper.Count(id => LevelOf(id) == level).Should().Be(needs[index], $"vòng {round}, mức {level}");
                }

                var counts = Pool(level, pools[index]).Select(id => usage.GetValueOrDefault(id)).ToList();

                if (needs[index] == 0)
                {
                    counts.Should().OnlyContain(count => count == 0);
                    continue;
                }

                (counts.Max() - counts.Min()).Should().BeLessThanOrEqualTo(1, $"vòng {round}, mức {level}: dùng không đều");
                counts.Max().Should().Be(levelPlan.MaxCopies, $"vòng {round}, mức {level}");
                counts.Count(count => count >= 2).Should().Be(levelPlan.SharedQuestions, $"vòng {round}, mức {level}");
                counts.Count(count => count == 0).Should().Be(levelPlan.UnusedQuestions, $"vòng {round}, mức {level}");
            }

            foreach (var paper in papers)
            {
                paper.Distinct().Should().HaveCount(paper.Count, $"vòng {round}: đề có câu trùng nhau");
            }
        }
    }
}

public class PaperAssignerTests
{
    [Fact]
    public void Phat_deu_cac_de_cho_lop_moi()
    {
        var assigned = PaperAssigner.Assign([0, 0, 0, 0], newcomers: 40, new Random(3));

        assigned.GroupBy(index => index).Select(group => group.Count()).Should().AllBeEquivalentTo(10);
    }

    [Fact]
    public void Lop_khong_chia_het_thi_cac_de_chenh_nhau_toi_da_mot_em()
    {
        var assigned = PaperAssigner.Assign([0, 0, 0, 0], newcomers: 23, new Random(5));

        var perPaper = Enumerable.Range(0, 4).Select(index => assigned.Count(value => value == index)).ToList();

        perPaper.Sum().Should().Be(23);
        (perPaper.Max() - perPaper.Min()).Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public void Giao_them_hoc_sinh_vao_lop_dang_co_nguoi_thi_bu_vao_de_it_nguoi_nhat()
    {
        // Đề 0 đang có 6 em, đề 1 có 3, đề 2 có 3, đề 3 có 4: 5 em mới phải đổ vào 1, 2 (rồi 3, 1, 2…).
        var assigned = PaperAssigner.Assign([6, 3, 3, 4], newcomers: 5, new Random(11));

        assigned.Should().NotContain(0);
        var final = new[] { 6, 3, 3, 4 }.Select((count, index) => count + assigned.Count(value => value == index)).ToList();
        (final.Max() - final.Min()).Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public void De_cua_em_dau_tien_khong_co_quy_luat_co_dinh()
    {
        // Phát theo vòng A,B,C,D thì em đầu tiên luôn nhận đề 0. Ở đây phải đổi theo nguồn ngẫu nhiên.
        var firsts = Enumerable.Range(0, 60)
            .Select(seed => PaperAssigner.Assign([0, 0, 0, 0], newcomers: 8, new Random(seed))[0])
            .ToHashSet();

        firsts.Should().HaveCount(4, "em đầu tiên có thể nhận bất cứ đề nào");
    }

    [Fact]
    public void Khong_co_de_nao_thi_bao_loi()
    {
        var act = () => PaperAssigner.Assign([], newcomers: 3, new Random(1));

        act.Should().Throw<ArgumentException>();
    }
}

public class PaperPlannerTests
{
    private static PaperPlan Plan(int easy, int medium, int hard, int papers, int easyEach, int mediumEach, int hardEach) =>
        PaperPlanner.Evaluate(
            new Dictionary<QuestionDifficulty, int>
            {
                [QuestionDifficulty.Easy] = easy,
                [QuestionDifficulty.Medium] = medium,
                [QuestionDifficulty.Hard] = hard
            },
            new Dictionary<QuestionDifficulty, int>
            {
                [QuestionDifficulty.Easy] = easyEach,
                [QuestionDifficulty.Medium] = mediumEach,
                [QuestionDifficulty.Hard] = hardEach
            },
            papers);

    [Fact]
    public void Vi_du_110_cau_chia_4_de_x_30_cau_thieu_3_3_4_cau_nen_trung_chung_do()
    {
        var plan = Plan(37, 37, 36, papers: 4, 10, 10, 10);

        plan.Feasible.Should().BeTrue();
        plan.PerPaperTotal.Should().Be(30);
        plan.Levels.Select(level => level.SharedQuestions).Should().Equal(3, 3, 4);
        plan.Levels.Select(level => level.MaxCopies).Should().Equal(2, 2, 2);
        plan.Levels.Select(level => level.UnusedQuestions).Should().Equal(0, 0, 0);
        plan.SharedQuestions.Should().Be(10);
    }

    [Fact]
    public void Vi_du_110_cau_chia_4_de_x_20_cau_khong_trung_con_du()
    {
        var plan = Plan(37, 37, 36, papers: 4, 7, 7, 6);

        plan.Feasible.Should().BeTrue();
        plan.SharedQuestions.Should().Be(0);
        plan.Levels.Select(level => level.UnusedQuestions).Should().Equal(9, 9, 12);
    }

    [Fact]
    public void De_lon_chia_dung_het_thi_khong_trung_khong_du()
    {
        var plan = Plan(40, 40, 40, papers: 4, 10, 10, 10);

        plan.SharedQuestions.Should().Be(0);
        plan.Levels.Should().OnlyContain(level => level.UnusedQuestions == 0 && level.MaxCopies == 1);
    }

    [Fact]
    public void Thieu_cau_tung_muc_thi_khong_kha_thi_va_bao_dung_muc()
    {
        var plan = Plan(37, 37, 8, papers: 4, 10, 10, 10);

        plan.Feasible.Should().BeFalse();
        var problems = PaperPlanner.Problems(plan);
        problems.Should().ContainSingle();
        problems[0].Should().Contain("Khó").And.Contain("10 câu").And.Contain("8 câu");
    }

    [Fact]
    public void Muc_khong_can_cau_nao_thi_toan_bo_cau_cua_muc_do_la_cau_du()
    {
        var plan = Plan(10, 6, 10, papers: 2, 5, 0, 5);

        plan.Feasible.Should().BeTrue();
        plan.Levels[1].UnusedQuestions.Should().Be(6);
    }

    [Fact]
    public void Cac_cau_cung_muc_phai_cung_diem()
    {
        var problems = PaperPlanner.PointProblems(
        [
            (QuestionDifficulty.Easy, 0.5m),
            (QuestionDifficulty.Easy, 0.5m),
            (QuestionDifficulty.Medium, 1m),
            (QuestionDifficulty.Medium, 1.5m),
            (QuestionDifficulty.Hard, 2m)
        ]);

        problems.Should().ContainSingle();
        problems[0].Should().Contain("Trung bình");
    }

    [Fact]
    public void Diem_dong_deu_trong_tung_muc_thi_hop_le()
    {
        PaperPlanner.PointProblems(
        [
            (QuestionDifficulty.Easy, 0.5m),
            (QuestionDifficulty.Medium, 1m),
            (QuestionDifficulty.Medium, 1m)
        ]).Should().BeEmpty();
    }
}
