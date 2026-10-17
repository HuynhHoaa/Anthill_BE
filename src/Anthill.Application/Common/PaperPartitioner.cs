using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>
/// Chia một "đề lớn" (danh sách câu hỏi giáo viên đã duyệt) thành N đề nhỏ có cùng số câu và cùng số câu từng mức độ,
/// cho phép các đề trùng câu khi đề lớn không đủ để chia riêng.
/// </summary>
/// <remarks>
/// <para>
/// Thứ tự ưu tiên khi đề lớn không chia hết: <b>(1)</b> mỗi đề đủ số câu yêu cầu, <b>(2)</b> mỗi đề đúng số câu từng mức
/// độ (tức đúng tỷ lệ), <b>(3)</b> ít trùng câu giữa các đề nhất có thể. Giới hạn cứng duy nhất: mỗi mức phải có ít nhất
/// bấy nhiêu câu trong đề lớn như <i>một</i> đề cần, nếu không một đề không đủ câu khác nhau — đó là việc của
/// <see cref="PaperPlanner"/> báo trước, hàm này chỉ ném <see cref="ArgumentException"/> nếu bị gọi sai.
/// </para>
/// <para>
/// <b>Chia phân tầng.</b> Mỗi mức độ xử lý riêng: xáo các câu của mức đó rồi phát lần lượt vào các đề theo vòng tròn.
/// Đề lớn đủ thì mỗi câu chỉ vào một đề. Không đủ thì danh sách quay lại từ đầu (xáo lại), nên mỗi câu được dùng đều tay —
/// số lần dùng của hai câu cùng mức chênh nhau tối đa một — và <b>không bao giờ có hai câu giống nhau trong cùng một
/// đề</b> (câu đã nằm trong đề đang xếp bị hoãn sang đề kế tiếp).
/// </para>
/// <para>
/// Là hàm thuần nhận <see cref="Random"/> từ ngoài vào: unit test truyền nguồn có hạt giống để kết quả lặp lại được, còn
/// production truyền nguồn khởi tạo bằng số ngẫu nhiên mật mã để học sinh không suy được bộ đề từ bên ngoài.
/// </para>
/// </remarks>
public static class PaperPartitioner
{
    /// <summary>Danh sách câu (mã câu hỏi) của từng đề, theo thứ tự đề 0, 1, 2… Trong mỗi đề: Dễ, rồi Trung bình, rồi Khó.</summary>
    public static IReadOnlyList<IReadOnlyList<int>> Partition(
        IReadOnlyList<int> easyPool,
        IReadOnlyList<int> mediumPool,
        IReadOnlyList<int> hardPool,
        int paperCount,
        int easyPerPaper,
        int mediumPerPaper,
        int hardPerPaper,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(easyPool);
        ArgumentNullException.ThrowIfNull(mediumPool);
        ArgumentNullException.ThrowIfNull(hardPool);
        ArgumentNullException.ThrowIfNull(random);

        if (paperCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(paperCount), "Cần ít nhất một đề.");
        }

        var papers = Enumerable.Range(0, paperCount).Select(_ => new List<int>()).ToList();

        var levels = new[]
        {
            (Pool: easyPool, Need: easyPerPaper),
            (Pool: mediumPool, Need: mediumPerPaper),
            (Pool: hardPool, Need: hardPerPaper)
        };

        foreach (var (pool, need) in levels)
        {
            var dealt = DealLevel(pool, paperCount, need, random);
            for (var index = 0; index < paperCount; index++)
            {
                papers[index].AddRange(dealt[index]);
            }
        }

        return papers;
    }

    private static List<List<int>> DealLevel(IReadOnlyList<int> pool, int paperCount, int perPaper, Random random)
    {
        var papers = Enumerable.Range(0, paperCount).Select(_ => new List<int>(perPaper)).ToList();

        if (perPaper == 0)
        {
            return papers;
        }

        if (pool.Count < perPaper)
        {
            throw new ArgumentException(
                $"Mỗi đề cần {perPaper} câu nhưng mức này chỉ có {pool.Count} câu.", nameof(pool));
        }

        if (pool.Distinct().Count() != pool.Count)
        {
            throw new ArgumentException("Một câu hỏi không được xuất hiện hai lần trong đề lớn.", nameof(pool));
        }

        var queue = Shuffled(pool, random);

        foreach (var paper in papers)
        {
            var inPaper = new HashSet<int>();

            while (paper.Count < perPaper)
            {
                var index = queue.FindIndex(id => !inPaper.Contains(id));

                if (index < 0)
                {
                    // Hết vòng: nạp vòng mới gồm những câu đã được dùng ở vòng này (câu còn nằm trong hàng đợi vẫn
                    // thuộc vòng cũ). Mỗi vòng cho mỗi câu đúng một lần nên số lần dùng giữa các câu không lệch quá một.
                    var queued = new HashSet<int>(queue);
                    queue.AddRange(Shuffled(pool.Where(id => !queued.Contains(id)).ToList(), random));
                    continue;
                }

                var chosen = queue[index];
                queue.RemoveAt(index);
                inPaper.Add(chosen);
                paper.Add(chosen);
            }
        }

        return papers;
    }

    /// <summary>Bản sao đã xáo (Fisher-Yates) bằng nguồn ngẫu nhiên đã cho.</summary>
    public static List<T> Shuffled<T>(IReadOnlyList<T> source, Random random)
    {
        var items = source.ToList();

        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapWith = random.Next(index + 1);
            (items[index], items[swapWith]) = (items[swapWith], items[index]);
        }

        return items;
    }

    /// <summary>
    /// Nguồn ngẫu nhiên khởi tạo bằng số ngẫu nhiên mật mã, cho việc chia đề và phát đề thật. Hạt giống không bao giờ
    /// được lưu hay suy ra từ mã đề, mã học sinh: kết quả chia đã nằm trong database, không cần dựng lại.
    /// </summary>
    public static Random CreateSecureRandom() =>
        new(System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue));
}

/// <summary>
/// Phát các đề cho học sinh: <b>ngẫu nhiên nhưng cân bằng</b>, và không để lộ quy luật.
/// </summary>
/// <remarks>
/// Phát theo thứ tự danh sách (A, B, C, D, A…) thì học sinh đoán được đề của mình từ chỗ đứng của mình trong danh sách,
/// và đoán được đề của bạn bên cạnh. Ở đây mỗi em lần lượt nhận một đề trong số những đề đang có ít em nhất, hoà thì bốc
/// ngẫu nhiên; người gọi nên xáo thứ tự học sinh trước, để chỗ đứng trong danh sách không liên quan gì tới đề nhận được.
/// Kết quả luôn cân bằng: số em của hai đề chênh nhau tối đa một, kể cả khi giao thêm học sinh vào lớp đã có người.
/// </remarks>
public static class PaperAssigner
{
    /// <param name="existingCounts">Số em đang cầm từng đề của lớp này (độ dài = số đề).</param>
    /// <param name="newcomers">Số em mới cần nhận đề.</param>
    /// <returns>Chỉ số đề (từ 0) của từng em mới, theo thứ tự đã đưa vào.</returns>
    public static IReadOnlyList<int> Assign(IReadOnlyList<int> existingCounts, int newcomers, Random random)
    {
        ArgumentNullException.ThrowIfNull(existingCounts);
        ArgumentNullException.ThrowIfNull(random);

        if (existingCounts.Count == 0)
        {
            throw new ArgumentException("Cần ít nhất một đề để phát.", nameof(existingCounts));
        }

        var counts = existingCounts.ToArray();
        var result = new List<int>(newcomers);

        for (var student = 0; student < newcomers; student++)
        {
            var lowest = counts.Min();
            var candidates = Enumerable.Range(0, counts.Length).Where(index => counts[index] == lowest).ToList();
            var picked = candidates[random.Next(candidates.Count)];

            counts[picked]++;
            result.Add(picked);
        }

        return result;
    }
}

/// <summary>Số liệu của một mức độ khi chia đề lớn thành các đề nhỏ.</summary>
/// <param name="PoolSize">Số câu của mức này trong đề lớn.</param>
/// <param name="PerPaper">Số câu mỗi đề cần ở mức này.</param>
/// <param name="Demand">Tổng số lượt câu cần cho mọi đề (số đề × số câu mỗi đề).</param>
/// <param name="Sufficient">Đề lớn có đủ câu để một đề có các câu khác nhau (<c>PoolSize ≥ PerPaper</c>).</param>
/// <param name="MaxCopies">Một câu bị dùng nhiều nhất ở bao nhiêu đề.</param>
/// <param name="SharedQuestions">Số câu nằm ở từ hai đề trở lên.</param>
/// <param name="UnusedQuestions">Số câu của đề lớn không nằm ở đề nhỏ nào.</param>
public sealed record PaperLevelPlan(
    QuestionDifficulty Level,
    int PoolSize,
    int PerPaper,
    int Demand,
    bool Sufficient,
    int MaxCopies,
    int SharedQuestions,
    int UnusedQuestions);

/// <summary>Kết quả tính trước việc chia: có chia được không, và mỗi mức trùng/dư bao nhiêu câu.</summary>
public sealed record PaperPlan(int PaperCount, IReadOnlyList<PaperLevelPlan> Levels)
{
    public bool Feasible => Levels.All(level => level.Sufficient);

    public int PerPaperTotal => Levels.Sum(level => level.PerPaper);

    public int SharedQuestions => Levels.Sum(level => level.SharedQuestions);
}

/// <summary>
/// Tính trước (không rút ngẫu nhiên) việc chia đề lớn thành N đề: kiểm khả thi, đếm số câu bị dùng ở nhiều đề và số câu dư.
/// Giao diện và server dùng cùng phép tính này, nên con số giáo viên thấy lúc soạn là con số lúc sinh đề.
/// </summary>
public static class PaperPlanner
{
    /// <summary>Trần số đề. Quá mức này thì không còn là "vài đề cho một lớp" mà là mỗi em một đề — đã có tính năng riêng.</summary>
    public const int MaxPaperCount = 10;

    private static readonly QuestionDifficulty[] Levels =
    [
        QuestionDifficulty.Easy,
        QuestionDifficulty.Medium,
        QuestionDifficulty.Hard
    ];

    public static PaperPlan Evaluate(
        IReadOnlyDictionary<QuestionDifficulty, int> poolCounts,
        IReadOnlyDictionary<QuestionDifficulty, int> perPaper,
        int paperCount)
    {
        ArgumentNullException.ThrowIfNull(poolCounts);
        ArgumentNullException.ThrowIfNull(perPaper);

        var plans = Levels.Select(level =>
        {
            var pool = poolCounts.GetValueOrDefault(level);
            var need = Math.Max(0, perPaper.GetValueOrDefault(level));
            var demand = paperCount * need;

            if (need == 0)
            {
                return new PaperLevelPlan(level, pool, 0, 0, true, 0, 0, pool);
            }

            if (pool < need)
            {
                return new PaperLevelPlan(level, pool, need, demand, false, 0, 0, 0);
            }

            // Mỗi câu được dùng đều: demand = quotient × pool + remainder, nên `remainder` câu dùng quotient + 1 lần
            // và số còn lại dùng quotient lần.
            var quotient = demand / pool;
            var remainder = demand % pool;

            var maxCopies = remainder > 0 ? quotient + 1 : quotient;
            var shared = quotient >= 2 ? pool : quotient == 1 ? remainder : 0;
            var unused = quotient == 0 ? pool - remainder : 0;

            return new PaperLevelPlan(level, pool, need, demand, true, maxCopies, shared, unused);
        }).ToList();

        return new PaperPlan(paperCount, plans);
    }

    /// <summary>Lỗi bằng tiếng Việt của một kế hoạch không chia được; rỗng khi chia được.</summary>
    public static IReadOnlyList<string> Problems(PaperPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.Levels
            .Where(level => !level.Sufficient)
            .Select(level =>
                $"Mức {LevelLabel(level.Level)}: mỗi đề cần {level.PerPaper} câu nhưng đề lớn chỉ có {level.PoolSize} câu "
                + $"{LevelLabel(level.Level)}. Hãy thêm câu {LevelLabel(level.Level)} vào đề, hoặc giảm số câu mỗi đề / tỷ lệ mức này.")
            .ToList();
    }

    /// <summary>
    /// Các câu cùng mức độ phải cùng điểm, để mọi đề có cùng tổng điểm. Trả về lỗi bằng tiếng Việt cho từng mức vi phạm.
    /// </summary>
    public static IReadOnlyList<string> PointProblems(IEnumerable<(QuestionDifficulty Difficulty, decimal Point)> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .GroupBy(item => item.Difficulty)
            .Where(group => group.Select(item => item.Point).Distinct().Count() > 1)
            .OrderBy(group => (int)group.Key)
            .Select(group =>
                $"Đề nhiều đề: các câu {LevelLabel(group.Key)} phải cùng điểm để mọi đề có cùng tổng điểm "
                + $"(đang có {group.Select(item => item.Point).Distinct().Count()} mức điểm khác nhau).")
            .ToList();
    }

    private static string LevelLabel(QuestionDifficulty level) => level switch
    {
        QuestionDifficulty.Easy => "Dễ",
        QuestionDifficulty.Medium => "Trung bình",
        _ => "Khó"
    };
}
