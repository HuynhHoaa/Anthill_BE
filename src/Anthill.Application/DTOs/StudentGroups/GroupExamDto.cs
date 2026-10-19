namespace Anthill.Application.DTOs.StudentGroups;

/// <summary>
/// Một bài kiểm tra đã giao cho lớp, tóm tắt theo lớp — <c>GET /api/student-groups/{id}/exams</c>, quản trị viên và giáo viên đang dạy lớp đọc.
/// </summary>
/// <remarks>
/// Đây là lịch sử học của lớp, không phải bảng theo dõi của một đề: số liệu chỉ tính những em được giao <b>qua lớp này</b>,
/// và đề cũ đã đóng vẫn có mặt. Bài của một em đã rời lớp mà em chưa làm thì đã bị thu hồi nên không đếm; bài em đã làm
/// thì được giữ (xem <c>MemberChangeResponse</c>) và vẫn đếm ở đây.
/// </remarks>
public sealed record GroupExamDto
{
    public required int ExamId { get; init; }

    public required string Title { get; init; }

    /// <summary>Giáo viên đã soạn đề — không nhất thiết là giáo viên phụ trách lớp hiện nay.</summary>
    public required string TeacherName { get; init; }

    /// <summary>Khung giờ của lớp: lịch riêng của lớp nếu có, ngược lại lịch chung của đề. ISO 8601 UTC.</summary>
    public required DateTime OpenAt { get; init; }

    public required DateTime CloseAt { get; init; }

    /// <summary><c>"scheduled"</c> · <c>"open"</c> · <c>"closed"</c> theo khung giờ của lớp.</summary>
    public required string Lifecycle { get; init; }

    /// <summary>Số em được giao bài này qua lớp.</summary>
    public required int AssignedCount { get; init; }

    /// <summary>Số em đã bắt đầu làm (có lượt làm bài, kể cả đang làm dở).</summary>
    public required int StartedCount { get; init; }

    /// <summary>Số em đã nộp bài (đã nộp hoặc đã chấm).</summary>
    public required int SubmittedCount { get; init; }

    /// <summary>Điểm trung bình các bài đã chấm; vắng mặt khi chưa có bài nào được chấm.</summary>
    public decimal? AverageScore { get; init; }

    /// <summary>Thang điểm của đề (tổng điểm tối đa); vắng mặt khi chưa có bài nào được chấm.</summary>
    public decimal? MaxScore { get; init; }

    /// <summary>Lần giao đầu tiên cho lớp này.</summary>
    public required DateTime AssignedAt { get; init; }
}
