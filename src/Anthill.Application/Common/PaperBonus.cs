using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>
/// Điểm cộng thêm để mỗi đề nhỏ cộng đúng 10.
/// </summary>
/// <remarks>
/// Điểm của một câu chỉ có hai chữ số thập phân và các câu cùng mức độ cùng điểm, nên tổng một đề luôn là bội của ước chung lớn
/// nhất của ba số câu × 0,01 — số câu như 3 / 3 / 3 không bao giờ ra đúng 10,00. Thay vì bắt giáo viên đổi số câu, đề lưu thêm
/// <c>Exam.PaperBonusUnits</c>: số đơn vị 0,01 điểm được cộng cho các câu <b>mức cao nhất</b> của <b>mỗi</b> bản đề nhỏ. Mọi bản đề
/// nhận đúng cùng số đơn vị nên vẫn cùng một tổng điểm. Bản đề lưu điểm từng câu riêng (<c>ExamPaperQuestion.Point</c>), nên hai
/// câu cùng mức trong các bản đề khác nhau có thể hơn kém nhau 0,01 mà chấm điểm vẫn đúng.
/// </remarks>
public static class PaperBonus
{
    /// <summary>Một đơn vị điểm cộng thêm: 0,01 điểm.</summary>
    public const decimal Unit = 0.01m;

    /// <summary>
    /// Mã các câu nhận thêm <see cref="Unit"/>: lấy theo thứ tự trong bản đề, từ mức Khó xuống Trung bình rồi Dễ (mỗi câu nhiều nhất
    /// một đơn vị). Số đơn vị lớn hơn số câu thì chỉ phát được bấy nhiêu câu — người gọi đã kiểm trần này lúc lưu đề.
    /// </summary>
    public static IReadOnlySet<int> Recipients(
        IReadOnlyList<(int QuestionId, QuestionDifficulty Difficulty)> orderedQuestions,
        int bonusUnits)
    {
        ArgumentNullException.ThrowIfNull(orderedQuestions);

        var chosen = new HashSet<int>();
        if (bonusUnits <= 0)
        {
            return chosen;
        }

        foreach (var level in new[] { QuestionDifficulty.Hard, QuestionDifficulty.Medium, QuestionDifficulty.Easy })
        {
            foreach (var question in orderedQuestions.Where(item => item.Difficulty == level))
            {
                if (chosen.Count >= bonusUnits)
                {
                    return chosen;
                }

                chosen.Add(question.QuestionId);
            }
        }

        return chosen;
    }
}
