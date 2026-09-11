using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.ExamAttempts;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Mappings;

/// <summary>
/// Chuyển dữ liệu làm bài sang DTO phía học sinh.
/// </summary>
/// <remarks>
/// Ranh giới quan trọng nhất của cả bước 6 nằm ở file này: mọi thứ đi ra qua đây đều là kiểu
/// <b>không có</b> trường <c>isCorrect</c>. Một dòng map nhầm sang DTO của giáo viên là đủ để lộ
/// toàn bộ đáp án cho cả lớp đang thi (§3 quy tắc ①).
/// </remarks>
public static class ExamAttemptMappingExtensions
{
    /// <summary>
    /// Một câu trong bản đề, cắt bỏ phần đáp án đúng.
    /// </summary>
    /// <remarks>
    /// Thứ tự phương án đọc thẳng từ cột <c>Ordinal</c> đã chốt lúc sinh đề. Xáo lại ở đây nghĩa là
    /// mỗi lần học sinh tải lại trang sẽ thấy một thứ tự khác, và những lựa chọn đã lưu — vốn ghi
    /// theo id chứ không theo vị trí — sẽ nhảy lung tung trên màn hình (§5).
    /// </remarks>
    public static AttemptQuestionDto ToAttemptQuestionDto(this ExamPaperQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        return new AttemptQuestionDto
        {
            Id = question.Id,
            Ordinal = question.Ordinal,
            Content = question.ContentSnapshot,
            ImageUrl = question.ImageUrlSnapshot,
            Point = question.Point,
            Answers = question.Answers
                .OrderBy(answer => answer.Ordinal)
                .Select(answer => new AttemptAnswerOptionDto
                {
                    Id = answer.Id,
                    Content = answer.ContentSnapshot,
                    ImageUrl = answer.ImageUrlSnapshot
                })
                .ToList()
        };
    }

    /// <summary>Đồng hồ của một lượt làm bài.</summary>
    public static AttemptTimeDto ToTimeDto(this ExamAttempt attempt, DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        return new AttemptTimeDto
        {
            ServerNow = asOfUtc.ToUtc(),
            ExpiresAt = attempt.ExpiresAt.ToUtc(),
            RemainingSeconds = AttemptDeadline.RemainingSeconds(attempt.ExpiresAt, asOfUtc),
            Status = attempt.Status.ToAssignmentStatusString()
        };
    }

    /// <summary>
    /// Một dòng trong danh sách bài được giao của học sinh.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="assignment"/> phải được nạp kèm <c>Exam</c>, <c>Exam.CreatedBy</c> và
    /// <c>Attempt</c>. Thiếu bất kỳ phần nào thì hàm vẫn chạy nhưng trả về dữ liệu nghèo đi trong im
    /// lặng, nên repository của học sinh luôn include đủ cả ba.
    /// </para>
    /// <para>
    /// Điểm số bị chặn ngay tại đây theo <c>showResultAfterSubmit</c> (§3 quy tắc ⑥). Chặn ở hàm map
    /// chứ không ở controller vì đây là chỗ duy nhất mọi đường đi đều phải qua.
    /// </para>
    /// </remarks>
    public static StudentExamItemDto ToStudentExamItemDto(this ExamAssignment assignment, DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var exam = assignment.Exam;
        var attempt = assignment.Attempt;
        // Theo lịch của BẢN GHI GIAO BÀI, không theo lịch của đề: lớp này có thể được đặt một
        // khung giờ riêng, và đọc lịch chung sẽ mở bài sai giờ cho đúng những em cần nó nhất.
        var lifecycle = assignment.ResolveLifecycle(asOfUtc);
        var (effectiveOpenAt, effectiveCloseAt) = AssignmentSchedule.Resolve(assignment);
        var status = assignment.ResolveStatus(asOfUtc);

        // Cho xem điểm khi hội đủ ba điều kiện của §3 quy tắc ⑥ — bài của chính em (đã kiểm ở
        // service), đã chấm, và giáo viên bật cờ cho xem ngay.
        var canSeeScore = exam.ShowResultAfterSubmit && attempt is { Status: AssignmentStatus.Graded };

        // Bản đề đã sinh mới là số câu THẬT của em này. Đọc exam.TotalQuestions là đọc cấu hình
        // hiện tại của đề, mà cấu hình đó giáo viên sửa được sau khi đã giao — lúc đó màn hình
        // giới thiệu hứa 40 câu trong khi bản đề trong tay em chỉ có 30. Màn làm bài và phản hồi
        // lúc nộp đều lấy từ bản đề, nên để nguyên thì ba chỗ nói ba con số khác nhau.
        var totalQuestions = assignment.Paper?.Questions.Count is > 0
            ? assignment.Paper.Questions.Count
            : exam.TotalQuestions;

        return new StudentExamItemDto
        {
            AssignmentId = assignment.Id,
            ExamId = exam.Id,
            Title = exam.Title,

            // Frontend hiển thị thẳng hai chuỗi này, không kiểm NULL.
            Description = exam.Description ?? string.Empty,
            Topic = exam.Topic ?? string.Empty,

            DurationMinutes = exam.DurationMinutes,
            TotalQuestions = totalQuestions,
            // Hai mốc này là thứ học sinh nhìn thấy trên thẻ bài và dùng để đếm ngược, nên phải là
            // giờ THẬT của em đó — tức lịch riêng của lớp nếu có, ngược lại mới là lịch chung.
            OpenAt = effectiveOpenAt.ToUtc(),
            CloseAt = effectiveCloseAt.ToUtc(),
            TeacherName = exam.CreatedBy?.FullName ?? string.Empty,
            Lifecycle = lifecycle.ToLifecycleString(),
            Status = status.ToAssignmentStatusString(),
            CanStart = CanStart(lifecycle, attempt, asOfUtc),
            ShowResultAfterSubmit = exam.ShowResultAfterSubmit,
            AttemptId = attempt?.Id,
            StartedAt = attempt?.StartedAt.ToUtc(),

            // Chỉ có nghĩa với lượt đang chạy: hạn giờ của một bài đã nộp là con số đã hết vai trò,
            // mà hiện lên thì màn hình danh sách lại vẽ một đồng hồ đếm ngược bên cạnh bài đã xong.
            ExpiresAt = attempt is { Status: AssignmentStatus.InProgress } ? attempt.ExpiresAt.ToUtc() : null,

            SubmittedAt = attempt?.SubmittedAt.ToUtc(),
            Score = canSeeScore ? attempt!.Score : null,
            MaxScore = canSeeScore ? attempt!.MaxScore : null,
            CorrectCount = canSeeScore ? attempt!.CorrectCount : null,
            AssignedAt = assignment.AssignedAt.ToUtc()
        };
    }

    /// <summary>
    /// Nút "Vào thi" có bấm được không.
    /// </summary>
    /// <remarks>
    /// Cùng một luật với <see cref="AttemptStartPolicy"/> nhưng rút gọn về một câu trả lời có/không:
    /// hàm kia quyết định server làm gì, hàm này quyết định màn hình vẽ gì. Cả hai cùng đọc
    /// <c>ExpiresAt</c> trong database nên không lệch nhau được.
    /// </remarks>
    private static bool CanStart(ExamLifecycle lifecycle, ExamAttempt? attempt, DateTime asOfUtc) =>
        AttemptStartPolicy.Decide(lifecycle, attempt?.Status, attempt?.ExpiresAt, asOfUtc)
            is AttemptStartDecision.CreateNew or AttemptStartDecision.Resume;

    /// <summary>
    /// Ô lọc trạng thái ở danh sách bài của học sinh. <c>"all"</c>/bỏ trống là không lọc, giá trị lạ bị từ chối.
    /// </summary>
    /// <remarks>
    /// Đi qua <see cref="FilterValueParser"/> chứ không tự viết lại <c>Enum.TryParse</c>: bản viết
    /// tay thiếu <c>Enum.IsDefined</c> nhận cả <c>?status=42</c> rồi trả về một danh sách rỗng trông
    /// như thật.
    /// </remarks>
    public static bool TryParseAttemptStatusFilter(string? value, out AssignmentStatus? status) =>
        FilterValueParser.TryParseFilter(value, out status);
}
