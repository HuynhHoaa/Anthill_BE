namespace Anthill.Application.DTOs.MyLearning;

/// <summary>
/// Một lớp mà học sinh đang học, cho trang "Lớp học của tôi" — <c>GET /api/my/groups</c>. Chỉ lớp đã công bố; lớp đã khoá vẫn có mặt
/// (chỉ đọc) để em xem lại bài cũ.
/// </summary>
public sealed record MyGroupDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>Kỳ học của lớp; vắng khi lớp chưa phân kỳ (học sinh thấy ở mục "Chưa phân kỳ").</summary>
    public int? TermId { get; init; }

    public string? TermName { get; init; }

    /// <summary>Giáo viên phụ trách (mỗi lớp một người); rỗng khi lớp chưa có ai.</summary>
    public required string TeacherName { get; init; }

    /// <summary>Môn của giáo viên phụ trách; NULL khi giáo viên chưa được gán môn.</summary>
    public string? SubjectName { get; init; }

    public required int StudentCount { get; init; }

    public required bool IsLocked { get; init; }

    /// <summary>Số bài đã giao cho em qua lớp này.</summary>
    public required int AssignedCount { get; init; }

    /// <summary>Số bài em đã nộp.</summary>
    public required int CompletedCount { get; init; }

    /// <summary>Số bài đang mở mà em chưa nộp (kể cả bài đang làm dở): việc cần làm ngay.</summary>
    public required int OpenCount { get; init; }

    /// <summary>Số bài chưa tới giờ mở.</summary>
    public required int UpcomingCount { get; init; }

    /// <summary>Số bài đã hết hạn mà em chưa làm (tính 0 điểm).</summary>
    public required int ExpiredCount { get; init; }
}
