using Anthill.Application.DTOs.Exams;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>
/// Tổng điểm của một đề, so với tổng điểm chuẩn <see cref="TotalScore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Đề theo <b>ma trận</b> (tổng số câu và tỷ lệ ba mức độ): câu hỏi cụ thể chỉ xuất hiện khi sinh bản đề,
/// có thể khác nhau giữa các học sinh, nên điểm gắn theo <b>mức độ khó</b> chứ không theo từng câu: giáo
/// viên đặt điểm cho câu Dễ, Trung bình, Khó, và số câu mỗi mức do <see cref="BlueprintCalculator"/>
/// quyết định. Mọi bản đề của cùng một đề nhờ đó có cùng một tổng điểm.
/// Đề theo <b>danh sách câu hỏi</b> thì mỗi câu có điểm riêng và tổng là tổng của chúng.
/// </para>
/// <para>
/// <see cref="TotalScore"/> là tổng điểm <b>chuẩn</b>, không phải điều kiện bắt buộc ở server. Giáo viên
/// chọn số câu bất kỳ (56 câu chẳng hạn), và nhiều cấu hình không chia ra đúng 10,00 với hai chữ số
/// thập phân. Bắt đủ 10 thì giáo viên kẹt ở những số câu đó, nên server nhận mọi tổng hợp lệ và báo lại
/// tổng thật qua <c>ExamDto.TotalScore</c>; việc nhắc "chưa đủ 10, có chốt như vậy không" nằm ở giao diện,
/// lúc giáo viên lưu bài. Kết quả và thống kê quy về thang 10 theo điểm tối đa của chính từng đề, nên đề không
/// đủ 10 (kể cả đề cũ chấm trên 100) vẫn so sánh được với đề khác.
/// Chuẩn từng là 100; đổi sang 10 ngày 15/10/2026 theo yêu cầu của chủ sản phẩm (thang điểm 10 quen thuộc).
/// </para>
/// </remarks>
public static class ExamScoring
{
    /// <summary>
    /// Tổng điểm chuẩn của một bài kiểm tra, và cũng là thang của mọi điểm trung bình, điểm cao nhất, xếp loại.
    /// </summary>
    public const decimal TotalScore = 10m;

    public static decimal TotalOf(
        int totalQuestions,
        int easyPercentage,
        int mediumPercentage,
        decimal pointEasy,
        decimal pointMedium,
        decimal pointHard)
    {
        var counts = BlueprintCalculator.CalculateRequiredCounts(totalQuestions, easyPercentage, mediumPercentage);

        return counts[QuestionDifficulty.Easy] * pointEasy
               + counts[QuestionDifficulty.Medium] * pointMedium
               + counts[QuestionDifficulty.Hard] * pointHard;
    }

    public static decimal TotalOf(SaveExamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Đề theo danh sách: tổng là tổng điểm từng câu. Người gọi (validator) đã bảo đảm từng điểm nằm trong
        // trần trước khi cộng, nên phép cộng này không tràn.
        if (request.Questions is not null)
        {
            return request.Questions.Sum(item => item.Point);
        }

        return TotalOf(
            request.TotalQuestions,
            request.EasyPercentage,
            request.MediumPercentage,
            request.PointEasy,
            request.PointMedium,
            request.PointHard);
    }

    /// <summary>
    /// Điểm <paramref name="score"/> trên điểm tối đa <paramref name="maxScore"/> của chính đề đó, quy về thang
    /// <see cref="TotalScore"/>; <c>null</c> khi đề không có điểm tối đa nên không chia được.
    /// </summary>
    /// <remarks>
    /// Mọi con số tổng hợp (trung bình, cao nhất) phải đi qua phép này trước khi gộp: một em có đề cũ 9 điểm
    /// và đề mới 100 điểm thì trung bình của hai điểm thô không có nghĩa gì. Truy vấn SQL ở
    /// <c>ExamAttemptRepository</c> làm cùng phép chia với cùng hằng <see cref="TotalScore"/>.
    /// </remarks>
    public static decimal? ToStandardScale(decimal score, decimal maxScore) =>
        maxScore > 0 ? score / maxScore * TotalScore : null;

    public static decimal TotalOf(Exam exam)
    {
        ArgumentNullException.ThrowIfNull(exam);

        // Đề theo danh sách: điểm từng câu có thể khác nhau trong cùng một mức nên không dựng lại được từ
        // ma trận; tổng được chụp lúc lưu danh sách.
        if (exam.UsesQuestionList)
        {
            return exam.ListTotalScore;
        }

        return TotalOf(
            exam.TotalQuestions,
            exam.EasyPercentage,
            exam.MediumPercentage,
            exam.PointEasy,
            exam.PointMedium,
            exam.PointHard);
    }

}
