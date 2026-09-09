using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.Exams;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Mappings;

/// <summary>
/// Chuyển <see cref="Exam"/> sang DTO, và một chỗ duy nhất định nghĩa "vòng đời của đề là gì".
/// </summary>
public static class ExamMappingExtensions
{
    public const string AllFilterValue = "all";
    public const string DraftLifecycleValue = "draft";

    /// <summary>
    /// Suy vòng đời từ cờ nháp và hai mốc thời gian.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Đây là phép tính mà §5 nói rõ <b>không được</b> biến thành cột lưu cứng. Lưu cứng nghĩa là
    /// phải có ai đó đi cập nhật trạng thái khi đồng hồ điểm — một background job, hoặc tệ hơn là
    /// một lần cập nhật lúc đọc — và trong khoảng thời gian giữa hai lần chạy thì database nói
    /// một đằng, thực tế một nẻo.
    /// </para>
    /// <para>
    /// <paramref name="asOfUtc"/> được truyền vào chứ không đọc <c>DateTime.UtcNow</c> tại chỗ:
    /// một danh sách mười đề phải được đánh giá theo <b>cùng một thời điểm</b>, nếu không hai dòng
    /// nằm sát mốc <c>closeAt</c> có thể rơi vào hai trạng thái khác nhau trong cùng một response.
    /// </para>
    /// </remarks>
    public static ExamLifecycle ResolveLifecycle(this Exam exam, DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(exam);

        if (exam.IsDraft)
        {
            return ExamLifecycle.Draft;
        }

        var now = asOfUtc.ToUtc();

        if (now < exam.OpenAt.ToUtc())
        {
            return ExamLifecycle.Scheduled;
        }

        return now > exam.CloseAt.ToUtc() ? ExamLifecycle.Closed : ExamLifecycle.Open;
    }

    public static ExamDto ToDto(this Exam exam, DateTime asOfUtc, int assignedCount, int submittedCount)
    {
        ArgumentNullException.ThrowIfNull(exam);

        return new ExamDto
        {
            Id = exam.Id,
            Title = exam.Title,

            // Frontend hiển thị thẳng hai chuỗi này, không kiểm NULL — nên quy đổi ở đây một lần
            // thay vì rải `?? ''` khắp các trang.
            Description = exam.Description ?? string.Empty,
            Topic = exam.Topic ?? string.Empty,

            DurationMinutes = exam.DurationMinutes,
            TotalQuestions = exam.TotalQuestions,
            EasyPercentage = exam.EasyPercentage,
            MediumPercentage = exam.MediumPercentage,
            HardPercentage = exam.HardPercentage,
            PointPerQuestion = exam.PointPerQuestion,
            ShuffleQuestions = exam.ShuffleQuestions,
            ShuffleAnswers = exam.ShuffleAnswers,
            RandomizePerStudent = exam.RandomizePerStudent,
            ShowResultAfterSubmit = exam.ShowResultAfterSubmit,
            OpenAt = exam.OpenAt.ToUtc(),
            CloseAt = exam.CloseAt.ToUtc(),
            Lifecycle = exam.ResolveLifecycle(asOfUtc).ToLifecycleString(),
            AssignedCount = assignedCount,
            SubmittedCount = submittedCount,
            CreatedAt = exam.CreatedAt.ToUtc()
        };
    }

    public static string ToLifecycleString(this ExamLifecycle lifecycle) => lifecycle switch
    {
        ExamLifecycle.Draft => DraftLifecycleValue,
        ExamLifecycle.Scheduled => "scheduled",
        ExamLifecycle.Open => "open",
        ExamLifecycle.Closed => "closed",
        _ => throw new ArgumentOutOfRangeException(nameof(lifecycle), lifecycle, null)
    };

    /// <summary>Khoá của hai từ điển trong <see cref="BlueprintPreviewDto"/>: viết thường, khớp frontend.</summary>
    public static string ToDifficultyKey(this QuestionDifficulty difficulty) => difficulty switch
    {
        QuestionDifficulty.Easy => "easy",
        QuestionDifficulty.Medium => "medium",
        QuestionDifficulty.Hard => "hard",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null)
    };

    /// <summary>Nhãn tiếng Việt của mức độ, dùng trong câu báo lỗi thiếu câu hỏi.</summary>
    public static string ToDifficultyLabel(this QuestionDifficulty difficulty) => difficulty switch
    {
        QuestionDifficulty.Easy => "Dễ",
        QuestionDifficulty.Medium => "Trung bình",
        QuestionDifficulty.Hard => "Khó",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null)
    };

    /// <summary>
    /// Ô lọc vòng đời: <c>"all"</c> và chuỗi rỗng đều thành NULL. Giá trị lạ trả về false để
    /// service từ chối, thay vì âm thầm bỏ lọc — trả nhiều hơn người dùng yêu cầu mà vẫn kèm mã
    /// 200 là kiểu sai không ai phát hiện.
    /// </summary>
    public static bool TryParseLifecycleFilter(string? value, out ExamLifecycle? lifecycle)
    {
        lifecycle = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();

        if (string.Equals(trimmed, AllFilterValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Enum.TryParse<ExamLifecycle>(trimmed, ignoreCase: true, out var parsed))
        {
            return false;
        }

        lifecycle = parsed;
        return true;
    }

    /// <summary>Chủ đề: <c>"all"</c> và chuỗi rỗng đều thành NULL, ngoài ra trả về chuỗi đã trim.</summary>
    public static string? NormalizeTopic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return string.Equals(trimmed, AllFilterValue, StringComparison.OrdinalIgnoreCase) ? null : trimmed;
    }
}
