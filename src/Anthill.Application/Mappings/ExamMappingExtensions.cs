using Anthill.Application.Common;
using Anthill.Application.DTOs.Exams;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Mappings;

/// <summary>
/// Chuyển <see cref="Exam"/> sang DTO, và một chỗ duy nhất định nghĩa "vòng đời của đề là gì".
/// </summary>
public static class ExamMappingExtensions
{
    public const string AllFilterValue = FilterValueParser.AllFilterValue;
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

        return ResolveWindowLifecycle(exam.IsDraft, exam.OpenAt, exam.CloseAt, asOfUtc);
    }

    /// <summary>Phép so vòng đời dùng chung cho đề, cho từng lượt giao và cho khung giờ của một lớp.</summary>
    /// <remarks>
    /// Một chỗ duy nhất, vì ba nơi đọc cùng một khung giờ mà so theo ba cách thì màn hình giáo viên báo lớp
    /// "đang mở" trong khi học sinh của lớp đó bị chặn. Bằng giờ đóng vẫn còn mở; nháp thắng mọi mốc giờ.
    /// </remarks>
    public static ExamLifecycle ResolveWindowLifecycle(bool isDraft, DateTime openAt, DateTime closeAt, DateTime asOfUtc)
    {
        if (isDraft)
        {
            return ExamLifecycle.Draft;
        }

        var now = asOfUtc.ToUtc();

        if (now < openAt.ToUtc())
        {
            return ExamLifecycle.Scheduled;
        }

        return now > closeAt.ToUtc() ? ExamLifecycle.Closed : ExamLifecycle.Open;
    }

    /// <summary>
    /// Vòng đời của đề <b>dưới mắt một học sinh cụ thể</b>.
    /// </summary>
    /// <remarks>
    /// Khác bản trên ở đúng một điểm, và điểm đó quyết định tất cả: mốc thời gian lấy từ bản ghi
    /// giao bài chứ không từ đề. Hai lớp cùng làm một đề vào hai tiết khác nhau thì với lớp sáng
    /// đề đã đóng trong khi với lớp chiều nó còn chưa mở — cùng một đề, cùng một thời điểm.
    ///
    /// Trạng thái nháp vẫn đọc từ đề: một đề chưa xuất bản thì không lớp nào làm được, bất kể lịch
    /// riêng đặt ra sao.
    ///
    /// Mọi đường phía học sinh phải dùng bản này; bản nhận <see cref="Exam"/> ở trên chỉ dành cho
    /// góc nhìn của giáo viên về chính đề đó.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Exam chưa được nạp.</exception>
    public static ExamLifecycle ResolveLifecycle(this ExamAssignment assignment, DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        // Cờ nháp chỉ nằm trên đề, nên hàm này cần Exam kể cả khi bản ghi có đủ giờ riêng — khác
        // Resolve, vốn tính được lịch mà không cần đề trong trường hợp đó.
        var isDraft = AssignmentSchedule.RequireExam(assignment).IsDraft;
        var (openAt, closeAt) = AssignmentSchedule.Resolve(assignment);

        return ResolveWindowLifecycle(isDraft, openAt, closeAt, asOfUtc);
    }

    /// <param name="assignmentWindows">Khung giờ theo lớp đã dựng sẵn; bỏ trống khi đề chưa giao cho ai.</param>
    public static ExamDto ToDto(
        this Exam exam,
        DateTime asOfUtc,
        int assignedCount,
        int submittedCount,
        int startedCount,
        IReadOnlyList<AssignmentWindowDto>? assignmentWindows = null)
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
            StartedCount = startedCount,

            // Cùng một phép so với ExamEditPolicy: đã có lượt làm bài là khoá.
            BlueprintFrozen = startedCount > 0,
            AssignmentWindows = assignmentWindows ?? [],

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
    /// Ô lọc vòng đời. <c>"all"</c> và chuỗi rỗng nghĩa là không lọc; giá trị lạ bị từ chối.
    /// </summary>
    public static bool TryParseLifecycleFilter(string? value, out ExamLifecycle? lifecycle) =>
        FilterValueParser.TryParseFilter(value, out lifecycle);

    /// <summary>Chủ đề: <c>"all"</c> và chuỗi rỗng đều thành NULL, ngoài ra trả về chuỗi đã trim.</summary>
    public static string? NormalizeTopic(string? value) => FilterValueParser.NormalizeTextFilter(value);
}
