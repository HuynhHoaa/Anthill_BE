namespace EduQuiz.Application.Interfaces.Realtime;

/// <summary>
/// Sáu sự kiện của hub <c>/hubs/notifications</c> (§7). Giai đoạn này chỉ khai báo;
/// phần cài đặt bằng SignalR nằm ở <c>EduQuiz.Infrastructure/Realtime</c>.
/// </summary>
/// <remarks>
/// Hai điều ràng buộc mọi lời gọi ở đây:
/// <list type="bullet">
/// <item>Bản ghi <c>Notification</c> được tạo trong <b>cùng transaction</b> với hành động nghiệp vụ;
/// việc bắn realtime chỉ chạy <b>sau khi</b> transaction commit — bắn trước mà rollback thì
/// người dùng thấy thông báo về một việc chưa từng xảy ra.</item>
/// <item>Client không bao giờ được tạo thông báo. Sinh thông báo là việc của backend.</item>
/// </list>
/// </remarks>
public interface IRealtimeNotificationService
{
    /// <summary>Giáo viên giao bài — chỉ bắn khi <c>notify = true</c>. Người nhận: từng học sinh.</summary>
    Task ExamAssignedAsync(
        IEnumerable<int> studentUserIds,
        ExamAssignedEvent payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Học sinh nộp bài. Bắn cho giáo viên sở hữu đề và cả vào group <c>exam:{examId}</c>
    /// để màn hình theo dõi tự tăng <c>submittedCount</c>.
    /// </summary>
    Task ExamSubmittedAsync(
        int teacherUserId,
        ExamSubmittedEvent payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hết giờ, hệ thống tự nộp. Hai vai trò nhận <b>hai payload khác nhau</b>: học sinh cần biết
    /// bài của mình đã bị nộp, giáo viên cần biết em nào vừa được chấm.
    /// </summary>
    Task AutoSubmittedAsync(
        int studentUserId,
        AutoSubmittedStudentEvent studentPayload,
        int teacherUserId,
        AutoSubmittedTeacherEvent teacherPayload,
        CancellationToken cancellationToken = default);

    /// <summary>Bài sắp đóng. Người nhận: học sinh <b>chưa nộp</b> và giáo viên sở hữu đề.</summary>
    Task DeadlineSoonAsync(
        IEnumerable<int> studentUserIds,
        int teacherUserId,
        DeadlineSoonEvent payload,
        CancellationToken cancellationToken = default);

    /// <summary>Giáo viên sửa đề đã giao (đổi lịch, đổi thời lượng). Người nhận: học sinh đã được giao.</summary>
    Task ExamUpdatedAsync(
        IEnumerable<int> studentUserIds,
        ExamUpdatedEvent payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thay thế trực tiếp cho việc poll <c>GET /notifications/unread-count</c> — endpoint đang
    /// bị gọi lại mỗi lần đổi trang ở cả hai layout, nên là endpoint bị gọi nhiều nhất hệ thống (§7).
    /// </summary>
    Task UnreadCountChangedAsync(
        int userId,
        int unreadCount,
        CancellationToken cancellationToken = default);
}

public sealed record ExamAssignedEvent
{
    public required int ExamId { get; init; }
    public required int AssignmentId { get; init; }
    public required string ExamTitle { get; init; }
    public required string TeacherName { get; init; }
    public required int DurationMinutes { get; init; }
    public required DateTime OpenAt { get; init; }
    public required DateTime CloseAt { get; init; }
}

public sealed record ExamSubmittedEvent
{
    public required int ExamId { get; init; }
    public required int AttemptId { get; init; }
    public required int StudentId { get; init; }
    public required string StudentName { get; init; }
    public required string ExamTitle { get; init; }
    public required decimal Score { get; init; }
    public required decimal MaxScore { get; init; }
    public required DateTime SubmittedAt { get; init; }

    /// <summary>Số bài đã nộp của đề sau lần nộp này — màn hình theo dõi hiển thị thẳng, không phải hỏi lại.</summary>
    public required int SubmittedCount { get; init; }
}

public sealed record AutoSubmittedStudentEvent
{
    public required int ExamId { get; init; }
    public required int AttemptId { get; init; }
    public required string ExamTitle { get; init; }
    public required DateTime SubmittedAt { get; init; }

    /// <summary>NULL khi đề không cho xem kết quả ngay sau khi nộp (§3 quy tắc ⑥).</summary>
    public decimal? Score { get; init; }
}

public sealed record AutoSubmittedTeacherEvent
{
    public required int ExamId { get; init; }
    public required int AttemptId { get; init; }
    public required int StudentId { get; init; }
    public required string StudentName { get; init; }
    public required string ExamTitle { get; init; }
    public required decimal Score { get; init; }
    public required decimal MaxScore { get; init; }
    public required DateTime SubmittedAt { get; init; }
    public required int SubmittedCount { get; init; }
}

public sealed record DeadlineSoonEvent
{
    public required int ExamId { get; init; }
    public required string ExamTitle { get; init; }
    public required DateTime CloseAt { get; init; }

    /// <summary>Số phút còn lại tại thời điểm bắn, tính theo đồng hồ server.</summary>
    public required int MinutesRemaining { get; init; }
}

public sealed record ExamUpdatedEvent
{
    public required int ExamId { get; init; }
    public required string ExamTitle { get; init; }
    public required int DurationMinutes { get; init; }
    public required DateTime OpenAt { get; init; }
    public required DateTime CloseAt { get; init; }
}
