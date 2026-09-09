using EduQuiz.Application.DTOs.ExamAssignments;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Mappings;

/// <summary>
/// Chuyển <see cref="ExamAssignment"/> sang DTO, và một chỗ duy nhất định nghĩa "tiến trình làm
/// bài của một học sinh là gì".
/// </summary>
public static class ExamAssignmentMappingExtensions
{
    /// <summary>
    /// Suy trạng thái từ lượt làm bài và lịch của đề.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Không có cột nào lưu giá trị này. Đã có lượt làm bài thì trạng thái của lượt đó là câu trả
    /// lời; chưa có thì còn tuỳ đề đã đóng hay chưa.
    /// </para>
    /// <para>
    /// Nhánh <c>Expired</c> là chỗ đáng nói: một em chưa từng bắt đầu, sau khi đề đóng, nếu vẫn
    /// báo <c>assigned</c> thì bảng theo dõi đọc như thể em còn cơ hội làm bài. Giáo viên cần phân
    /// biệt "chưa làm nhưng còn kịp" với "hết giờ mà không vào thi" — đó là hai tình huống dẫn tới
    /// hai hành động khác nhau.
    /// </para>
    /// </remarks>
    public static AssignmentStatus ResolveStatus(this ExamAssignment assignment, DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (assignment.Attempt is not null)
        {
            return assignment.Attempt.Status;
        }

        // Đề còn là nháp thì chưa nói chuyện hết hạn được: nháp không có lịch nào đang chạy.
        if (assignment.Exam is { IsDraft: false } exam && asOfUtc.ToUtc() > exam.CloseAt.ToUtc())
        {
            return AssignmentStatus.Expired;
        }

        return AssignmentStatus.Assigned;
    }

    public static ExamAssignmentDto ToDto(this ExamAssignment assignment, DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var attempt = assignment.Attempt;
        var status = assignment.ResolveStatus(asOfUtc);

        return new ExamAssignmentDto
        {
            Id = assignment.Id,
            ExamId = assignment.ExamId,
            StudentId = assignment.StudentId,
            StudentName = assignment.Student?.FullName ?? string.Empty,
            StudentCode = assignment.Student?.Code ?? string.Empty,
            Group = assignment.Student?.Group?.Name ?? string.Empty,
            Status = status.ToAssignmentStatusString(),
            AttemptId = attempt?.Id,

            // Điểm chỉ có nghĩa sau khi đã chấm. Trả 0 cho một bài đang làm dở thì màn hình kết quả
            // hiện "0 điểm" y hệt một bài làm sai hết.
            Score = attempt is { Status: AssignmentStatus.Graded } ? attempt.Score : null,

            StartedAt = attempt?.StartedAt.ToUtc(),
            SubmittedAt = attempt?.SubmittedAt.ToUtc(),
            AssignedAt = assignment.AssignedAt.ToUtc()
        };
    }

    /// <summary>Chuỗi trạng thái viết thường, đúng như kiểu <c>ExamStatus</c> của frontend khai báo.</summary>
    public static string ToAssignmentStatusString(this AssignmentStatus status) => status switch
    {
        AssignmentStatus.Assigned => "assigned",
        AssignmentStatus.InProgress => "inprogress",
        AssignmentStatus.Submitted => "submitted",
        AssignmentStatus.Graded => "graded",
        AssignmentStatus.Expired => "expired",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
