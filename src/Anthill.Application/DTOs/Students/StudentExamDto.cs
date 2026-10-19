namespace Anthill.Application.DTOs.Students;

/// <summary>
/// Một bài kiểm tra đã giao cho một học sinh, kèm bài làm nếu em đã làm — <c>GET /api/students/{id}/exams</c>,
/// chỉ quản trị viên đọc.
/// </summary>
/// <remarks>
/// <para>
/// Khác danh sách kết quả của giáo viên ở chỗ liệt kê <b>cả bài em chưa làm</b>: em nghỉ học mà không báo cáo thì
/// các bài sau của lớp vẫn hiện ra ở đây với trạng thái "chưa làm" hoặc "hết hạn", và đó chính là điều người
/// quản lý cần nhìn thấy.
/// </para>
/// <para>
/// Quản trị viên xem như một người quan sát nên điểm luôn hiện, không bị chặn theo <c>showResultAfterSubmit</c> —
/// cờ đó chỉ nói về điều học sinh được thấy.
/// </para>
/// </remarks>
public sealed record StudentExamDto
{
    public required int AssignmentId { get; init; }

    public required int ExamId { get; init; }

    public required string Title { get; init; }

    public required string TeacherName { get; init; }

    /// <summary>Lớp đã giao bài này cho em; vắng mặt với lượt giao lẻ cũ hoặc khi lớp đã bị xoá.</summary>
    public string? Group { get; init; }

    /// <summary><c>assigned</c> (chưa làm, còn hạn), <c>inprogress</c>, <c>submitted</c>, <c>graded</c>, <c>expired</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Khung giờ thật của em: lịch riêng của lớp nếu có, ngược lại lịch chung của đề.</summary>
    public required DateTime OpenAt { get; init; }

    public required DateTime CloseAt { get; init; }

    public int? AttemptId { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? SubmittedAt { get; init; }

    /// <summary>Có khi bài đã chấm.</summary>
    public decimal? Score { get; init; }

    public decimal? MaxScore { get; init; }

    public int? CorrectCount { get; init; }

    public required DateTime AssignedAt { get; init; }
}
