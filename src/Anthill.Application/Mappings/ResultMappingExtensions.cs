using Anthill.Application.DTOs.Results;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Mappings;

/// <summary>
/// Chuyển lượt làm bài sang DTO kết quả, và là chỗ duy nhất định nghĩa "ai được xem điểm" và "ai
/// được xem đáp án".
/// </summary>
public static class ResultMappingExtensions
{
    /// <summary>
    /// Người gọi có được xem điểm của lượt này hay không — Score, MaxScore và CorrectCount.
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
    /// <para>
    /// Cổng này chỉ nói về điểm. Đáp án từng câu có cổng riêng là <see cref="ResolveAnswerVisibility"/>,
    /// vì hai thứ đó mở ở hai thời điểm khác nhau.
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

    /// <summary>Người gọi đã được xem đáp án đúng của từng câu chưa, và nếu chưa thì bao giờ.</summary>
    /// <remarks>
    /// <para>
    /// Học sinh phải qua <see cref="CanRevealScore"/> trước, rồi thêm một điều kiện: <b>mọi</b> lượt
    /// giao của đề đều đã qua giờ đóng. Một đề giao cho nhiều lớp, mỗi lớp một khung giờ; lớp sáng thứ
    /// Ba làm xong trước lớp chiều thứ Năm hai ngày, và mở đáp án ngay khi chấm là cho lớp sáng hai
    /// ngày để chuyền nó đi.
    /// </para>
    /// <para>
    /// Điểm vẫn mở ngay — quyết định của chủ sản phẩm. Số câu đúng là con số gộp, không chỉ ra câu
    /// nào; còn <c>isCorrect</c> từng câu ghép với lựa chọn của chính em là đọc ngược ra đáp án, nên
    /// nó đi theo cổng này.
    /// </para>
    /// <para>
    /// Hàm thuần, nhận mốc mở đáp án từ ngoài vào — phép tính giờ đóng muộn nhất chạy trong SQL ở
    /// repository — để kiểm được bằng unit test.
    /// </para>
    /// </remarks>
    /// <param name="answersReleaseAtUtc">Giờ đóng có hiệu lực muộn nhất trong mọi lượt giao của đề.</param>
    public static AnswerVisibility ResolveAnswerVisibility(
        this ExamAttempt attempt,
        bool isOwningTeacher,
        DateTime answersReleaseAtUtc,
        DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (isOwningTeacher)
        {
            return new AnswerVisibility(true, null);
        }

        // Chưa qua cổng điểm thì không có giờ nào để hẹn: bài chưa chấm, hoặc giáo viên tắt
        // showResultAfterSubmit — đáp án không tự mở vào lúc nào cả, hứa một mốc giờ là hứa sai.
        if (!attempt.CanRevealScore(isOwningTeacher: false))
        {
            return new AnswerVisibility(false, null);
        }

        var releaseAt = answersReleaseAtUtc.ToUtc();

        // So bằng ">" giống ResolveLifecycle: đúng giây đóng bài thì lớp đó vẫn còn đang mở.
        return asOfUtc.ToUtc() > releaseAt
            ? new AnswerVisibility(true, null)
            : new AnswerVisibility(false, releaseAt);
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

            // Vế dự phòng cùng lý do với ExamAssignmentMappingExtensions.ToDto: repository luôn nạp Student.
            StudentStatus = (student?.Status ?? UserStatus.Active).ToStatusString(),
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

/// <summary>Kết quả cổng xem đáp án: được xem chưa, và nếu chưa thì mở lúc nào.</summary>
/// <param name="CanSee">Được gắn <c>correctAnswerId</c>/<c>isCorrect</c> vào từng câu hay chưa.</param>
/// <param name="VisibleAt">NULL khi đã xem được, hoặc khi không có mốc nào để hẹn.</param>
public readonly record struct AnswerVisibility(bool CanSee, DateTime? VisibleAt);
