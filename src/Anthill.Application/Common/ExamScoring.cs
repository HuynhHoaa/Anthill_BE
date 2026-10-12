using Anthill.Application.DTOs.Exams;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>
/// Tổng điểm của một đề, so với tổng điểm chuẩn <see cref="TotalScore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Đề là một <b>ma trận</b> (tổng số câu và tỷ lệ ba mức độ), còn câu hỏi cụ thể chỉ xuất hiện khi sinh
/// bản đề, có thể khác nhau giữa các học sinh. Vì vậy điểm gắn theo <b>mức độ khó</b> chứ không theo
/// từng câu: giáo viên đặt điểm cho câu Dễ, Trung bình, Khó, và số câu mỗi mức do
/// <see cref="BlueprintCalculator"/> quyết định. Mọi bản đề của cùng một đề nhờ đó có cùng một tổng điểm.
/// </para>
/// <para>
/// <see cref="TotalScore"/> là tổng điểm <b>chuẩn</b>, không phải điều kiện bắt buộc ở server. Giáo viên
/// chọn số câu bất kỳ (56 câu chẳng hạn), và nhiều cấu hình không chia ra đúng 100,00 với hai chữ số
/// thập phân (30 câu 12/12/6: tổng luôn là bội của 0,06). Bắt đủ 100 thì giáo viên kẹt ở những số câu
/// đó, nên server nhận mọi tổng hợp lệ và báo lại tổng thật qua <c>ExamDto.TotalScore</c>; việc nhắc
/// "chưa đủ 100, có chốt như vậy không" nằm ở giao diện, lúc giáo viên lưu bài. Kết quả và thống kê quy
/// về thang 100 theo điểm tối đa của chính từng đề, nên đề không đủ 100 vẫn so sánh được với đề khác.
/// </para>
/// </remarks>
public static class ExamScoring
{
    /// <summary>Tổng điểm chuẩn của một bài kiểm tra; cũng là trần điểm của một câu.</summary>
    public const decimal TotalScore = 100m;

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

        return TotalOf(
            request.TotalQuestions,
            request.EasyPercentage,
            request.MediumPercentage,
            request.PointEasy,
            request.PointMedium,
            request.PointHard);
    }

    public static decimal TotalOf(Exam exam)
    {
        ArgumentNullException.ThrowIfNull(exam);

        return TotalOf(
            exam.TotalQuestions,
            exam.EasyPercentage,
            exam.MediumPercentage,
            exam.PointEasy,
            exam.PointMedium,
            exam.PointHard);
    }

}
