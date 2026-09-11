using Anthill.Domain.Entities;

namespace Anthill.Application.Common;

/// <summary>Kết quả chấm một lượt làm bài.</summary>
public sealed record AttemptScore
{
    public required int CorrectCount { get; init; }

    public required decimal Score { get; init; }

    /// <summary>Tổng điểm của bản đề, không phụ thuộc học sinh làm được bao nhiêu.</summary>
    public required decimal MaxScore { get; init; }

    /// <summary>Số câu học sinh có chọn đáp án — dùng để phân biệt "làm sai" với "bỏ trống".</summary>
    public required int AnsweredCount { get; init; }
}

/// <summary>
/// Chấm bài từ <b>bản chụp</b> trong <see cref="ExamPaperAnswer"/> (§3 quy tắc ②).
/// </summary>
/// <remarks>
/// <para>
/// Hàm thuần, không chạm database, không nhận gì từ client ngoài các cặp câu-đáp án đã được kiểm.
/// Điểm và số câu đúng <b>không bao giờ</b> đọc từ request: client chỉ nói em đã chọn ô nào.
/// </para>
/// <para>
/// Nguồn đối chiếu là bản chụp chứ không phải <c>AnswerOption</c> gốc. Mock của frontend tra ngược
/// ngân hàng câu hỏi theo id lúc chấm, nghĩa là giáo viên sửa một câu hôm nay sẽ làm đổi điểm những
/// bài đã chấm tuần trước — chấm từ snapshot là cách duy nhất để một bài đã chấm giữ nguyên ý nghĩa.
/// </para>
/// </remarks>
public static class AttemptGrading
{
    /// <summary>Số chữ số thập phân của cột <c>Score</c> trong database.</summary>
    private const int ScoreDecimals = 2;

    public static decimal MaxScoreOf(IEnumerable<ExamPaperQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);

        return Round(questions.Sum(question => question.Point));
    }

    /// <summary>
    /// Đối chiếu lựa chọn của học sinh với đáp án đã chụp.
    /// </summary>
    /// <param name="questions">Toàn bộ câu của bản đề, mỗi câu kèm phương án và cờ <c>IsCorrect</c>.</param>
    /// <param name="selections">Map <c>ExamPaperQuestionId → SelectedAnswerId</c>; thiếu khoá hoặc giá trị NULL đều là bỏ trống.</param>
    /// <remarks>
    /// Một id đáp án không thuộc câu đó thì tính là <b>sai</b> chứ không ném lỗi. Tầng service đã
    /// chặn trường hợp này từ lúc lưu nháp (§5, trả 422), nhưng hàm chấm là chốt chặn cuối cùng —
    /// nó không được phép tin rằng chốt chặn phía trước còn nguyên vẹn.
    /// </remarks>
    public static AttemptScore Grade(
        IEnumerable<ExamPaperQuestion> questions,
        IReadOnlyDictionary<int, int?> selections)
    {
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(selections);

        var correctCount = 0;
        var answeredCount = 0;
        var score = 0m;
        var maxScore = 0m;

        foreach (var question in questions)
        {
            maxScore += question.Point;

            if (!selections.TryGetValue(question.Id, out var selectedId) || selectedId is null)
            {
                continue;
            }

            answeredCount++;

            if (question.Answers.Any(answer => answer.Id == selectedId.Value && answer.IsCorrect))
            {
                correctCount++;
                score += question.Point;
            }
        }

        return new AttemptScore
        {
            CorrectCount = correctCount,
            AnsweredCount = answeredCount,
            Score = Round(score),
            MaxScore = Round(maxScore)
        };
    }

    /// <summary>
    /// <c>MidpointRounding.AwayFromZero</c> đồng bộ với <c>BlueprintCalculator</c>: cả hệ thống chỉ
    /// dùng một quy tắc làm tròn, và đó là quy tắc con người mong đợi khi nhìn một cột điểm.
    /// </summary>
    private static decimal Round(decimal value) => Math.Round(value, ScoreDecimals, MidpointRounding.AwayFromZero);
}
