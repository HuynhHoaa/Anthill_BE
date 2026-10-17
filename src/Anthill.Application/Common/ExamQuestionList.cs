using Anthill.Application.DTOs.Exams;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>Số liệu ma trận dẫn xuất từ một danh sách câu hỏi cụ thể.</summary>
/// <param name="TotalQuestions">Số câu của danh sách.</param>
/// <param name="EasyPercentage">Tỷ lệ câu Dễ, số nguyên; ba tỷ lệ cộng đúng 100 khi danh sách không rỗng.</param>
/// <param name="MediumPercentage">Tỷ lệ câu Trung bình.</param>
/// <param name="HardPercentage">Tỷ lệ câu Khó.</param>
/// <param name="PointEasy">Điểm trung bình của các câu Dễ, 0 khi không có câu Dễ nào.</param>
/// <param name="PointMedium">Điểm trung bình của các câu Trung bình.</param>
/// <param name="PointHard">Điểm trung bình của các câu Khó.</param>
/// <param name="TotalScore">Tổng điểm thật: tổng điểm từng câu.</param>
public sealed record DerivedBlueprint(
    int TotalQuestions,
    int EasyPercentage,
    int MediumPercentage,
    int HardPercentage,
    decimal PointEasy,
    decimal PointMedium,
    decimal PointHard,
    decimal TotalScore);

/// <summary>
/// Phép tính thuần cho đề soạn theo danh sách câu hỏi.
/// </summary>
/// <remarks>
/// <para>
/// Đề theo danh sách vẫn ghi các cột ma trận (số câu, tỷ lệ, điểm theo mức) để mọi chỗ đọc chúng — danh
/// sách đề, màn hình làm bài, thống kê — tiếp tục chạy mà không phải biết có hai kiểu đề. Các cột đó là
/// <b>số liệu dẫn xuất</b>, không phải đầu vào: nguồn thật là danh sách. Riêng tổng điểm thì đọc từ
/// <see cref="Exam.ListTotalScore"/>, vì điểm từng câu có thể khác nhau trong một mức nên không suy ngược được
/// từ điểm trung bình.
/// </para>
/// <para>
/// Là hàm thuần để kiểm bằng unit test, không cần database.
/// </para>
/// </remarks>
public static class ExamQuestionList
{
    private static readonly QuestionDifficulty[] LevelOrder =
    [
        QuestionDifficulty.Easy,
        QuestionDifficulty.Medium,
        QuestionDifficulty.Hard
    ];

    /// <summary>Dựng số liệu ma trận từ độ khó và điểm của từng câu trong danh sách.</summary>
    public static DerivedBlueprint Derive(IReadOnlyList<(QuestionDifficulty Difficulty, decimal Point)> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var counts = LevelOrder.ToDictionary(level => level, _ => 0);
        var sums = LevelOrder.ToDictionary(level => level, _ => 0m);

        foreach (var (difficulty, point) in items)
        {
            counts[difficulty]++;
            sums[difficulty] += point;
        }

        var percentages = Percentages(counts, items.Count);

        decimal Average(QuestionDifficulty difficulty) =>
            counts[difficulty] == 0 ? 0m : decimal.Round(sums[difficulty] / counts[difficulty], 2);

        return new DerivedBlueprint(
            items.Count,
            percentages[QuestionDifficulty.Easy],
            percentages[QuestionDifficulty.Medium],
            percentages[QuestionDifficulty.Hard],
            Average(QuestionDifficulty.Easy),
            Average(QuestionDifficulty.Medium),
            Average(QuestionDifficulty.Hard),
            sums.Values.Sum());
    }

    /// <summary>
    /// Ba tỷ lệ nguyên cộng đúng 100 (phần dư chia theo phần thập phân lớn nhất; hoà thì Dễ, Trung bình, Khó).
    /// Danh sách rỗng cho ba số không.
    /// </summary>
    private static Dictionary<QuestionDifficulty, int> Percentages(
        Dictionary<QuestionDifficulty, int> counts,
        int total)
    {
        var result = LevelOrder.ToDictionary(level => level, _ => 0);
        if (total == 0)
        {
            return result;
        }

        var remainders = new Dictionary<QuestionDifficulty, int>();

        foreach (var level in LevelOrder)
        {
            var scaled = counts[level] * 100;
            result[level] = scaled / total;
            remainders[level] = scaled % total;
        }

        var missing = 100 - result.Values.Sum();

        // OrderByDescending ổn định: hoà thì giữ thứ tự Dễ, Trung bình, Khó.
        foreach (var level in LevelOrder.OrderByDescending(level => remainders[level]).Take(missing))
        {
            result[level]++;
        }

        return result;
    }

    /// <summary>
    /// Danh sách gửi lên có khác danh sách đang lưu không — khác ở câu nào, thứ tự hay điểm đều tính.
    /// </summary>
    public static bool Differs(IReadOnlyList<ExamQuestion> current, IReadOnlyList<ExamQuestionInput> requested)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(requested);

        if (current.Count != requested.Count)
        {
            return true;
        }

        var ordered = current.OrderBy(row => row.Ordinal).ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].QuestionId != requested[index].QuestionId
                || ordered[index].Point != requested[index].Point)
            {
                return true;
            }
        }

        return false;
    }
}
