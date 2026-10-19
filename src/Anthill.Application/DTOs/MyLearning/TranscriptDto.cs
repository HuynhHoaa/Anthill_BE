namespace Anthill.Application.DTOs.MyLearning;

/// <summary>Bảng điểm của học sinh — <c>GET /api/my/transcript</c>: các môn em đang học kèm điểm trung bình từng môn.</summary>
public sealed record TranscriptDto
{
    /// <summary>Trung bình các điểm trung bình môn (thang 10); NULL khi chưa môn nào có điểm để tính.</summary>
    public decimal? OverallAverage { get; init; }

    public required IReadOnlyList<TranscriptSubjectDto> Subjects { get; init; }
}

/// <summary>Một môn: điểm trung bình và các bài của môn đó (của mọi lớp em học môn này).</summary>
public sealed record TranscriptSubjectDto
{
    /// <summary>Môn của giáo viên ra đề; "Chưa gán môn" khi giáo viên chưa có môn.</summary>
    public required string SubjectName { get; init; }

    /// <summary>Các lớp có bài của môn này, theo tên.</summary>
    public required IReadOnlyList<string> GroupNames { get; init; }

    /// <summary>
    /// Điểm trung bình thang 10. Tính trên các bài đã nộp và được xem điểm, cộng bài đã hết hạn mà em không làm (0 điểm). KHÔNG tính bài còn
    /// làm được (đang mở, đang làm, sắp mở) và bài đã nộp nhưng giáo viên chưa cho xem điểm. NULL khi chưa có bài nào để tính.
    /// </summary>
    public decimal? Average { get; init; }

    /// <summary>Số bài được tính vào điểm trung bình.</summary>
    public required int CountedExams { get; init; }

    public required int TotalExams { get; init; }

    /// <summary>Bài đã nộp nhưng chưa được xem điểm — không tính vào trung bình.</summary>
    public required int HiddenExams { get; init; }

    /// <summary>Bài còn làm được (đang mở hoặc đang làm dở) — chưa tính, tới hạn mới tính.</summary>
    public required int PendingExams { get; init; }

    public required int UpcomingExams { get; init; }

    /// <summary>Bài đã hết hạn mà em không làm — tính 0 điểm (nằm trong <see cref="CountedExams"/>).</summary>
    public required int MissedExams { get; init; }

    public required IReadOnlyList<TranscriptExamDto> Exams { get; init; }
}

/// <summary>Một bài trong bảng điểm.</summary>
public sealed record TranscriptExamDto
{
    public required int ExamId { get; init; }

    public required int AssignmentId { get; init; }

    public required string Title { get; init; }

    public int? GroupId { get; init; }

    public string? GroupName { get; init; }

    /// <summary><c>assigned</c> · <c>inprogress</c> · <c>submitted</c> · <c>graded</c> · <c>expired</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Điểm quy về thang 10; NULL khi chưa nộp hoặc chưa được xem điểm. Bài bỏ lỡ không có điểm ở đây (nhưng được tính 0).</summary>
    public decimal? Score { get; init; }

    /// <summary>Bài này có nằm trong điểm trung bình của môn hay không.</summary>
    public required bool Counted { get; init; }

    /// <summary>Có lượt làm bài để mở trang kết quả hay không (null khi em chưa vào làm).</summary>
    public int? AttemptId { get; init; }

    public DateTime? SubmittedAt { get; init; }

    public required DateTime CloseAt { get; init; }
}
