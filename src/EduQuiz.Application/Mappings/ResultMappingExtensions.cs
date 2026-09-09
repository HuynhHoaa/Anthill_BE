using EduQuiz.Application.DTOs.Results;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Mappings;

/// <summary>
/// Chuyển lượt làm bài sang DTO kết quả, và một chỗ duy nhất định nghĩa "ai được xem điểm".
/// </summary>
public static class ResultMappingExtensions
{
    /// <summary>
    /// Người gọi có được xem điểm và đáp án đúng của lượt này hay không.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Giáo viên sở hữu đề luôn được — họ là người chấm. Học sinh phải hội đủ <b>ba</b> điều kiện
    /// (§3 quy tắc ⑥): bài là của mình, bài đã chấm xong, và giáo viên bật
    /// <c>showResultAfterSubmit</c>.
    /// </para>
    /// <para>
    /// Quy tắc ba vế này nằm đúng một chỗ vì nó được hỏi ở bốn nơi khác nhau — bảng kết quả, chi
    /// tiết bài làm, tóm tắt sau khi nộp, và trang tổng quan. Chép nó ra bốn bản thì chỉ cần một
    /// bản quên mất vế thứ ba là điểm rời server sớm hơn giáo viên định.
    /// </para>
    /// </remarks>
    public static bool CanRevealScore(this ExamAttempt attempt, bool isOwningTeacher)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (isOwningTeacher)
        {
            return true;
        }

        return attempt.Status == AssignmentStatus.Graded
               && attempt.Assignment.Exam.ShowResultAfterSubmit;
    }

    public static ResultSummaryDto ToSummaryDto(this ExamAttempt attempt, bool canRevealScore, int totalQuestions)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        var assignment = attempt.Assignment;
        var exam = assignment.Exam;
        var student = assignment.Student;

        return new ResultSummaryDto
        {
            AttemptId = attempt.Id,
            AssignmentId = assignment.Id,
            ExamId = exam.Id,
            ExamTitle = exam.Title,
            Topic = exam.Topic ?? string.Empty,
            StudentId = assignment.StudentId,
            StudentName = student?.FullName ?? string.Empty,
            StudentCode = student?.Code ?? string.Empty,
            Group = student?.Group?.Name ?? string.Empty,
            Status = attempt.Status.ToAssignmentStatusString(),
            StartedAt = attempt.StartedAt.ToUtc(),
            SubmittedAt = attempt.SubmittedAt.ToUtc(),
            AutoSubmitted = attempt.AutoSubmitted,
            TotalQuestions = totalQuestions,

            // Ba trường điểm cùng sống hoặc cùng vắng. Trả MaxScore mà giấu Score thì người xem
            // biết thang điểm nhưng không biết kết quả — vô hại; còn trả CorrectCount mà giấu
            // Score thì họ tự nhân ra được điểm.
            Score = canRevealScore ? attempt.Score : null,
            MaxScore = canRevealScore ? attempt.MaxScore : null,
            CorrectCount = canRevealScore ? attempt.CorrectCount : null
        };
    }
}
