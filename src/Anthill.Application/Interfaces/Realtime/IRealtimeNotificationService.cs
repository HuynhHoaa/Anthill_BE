namespace Anthill.Application.Interfaces.Realtime;

/// <summary>
/// Sáu sự kiện của hub <c>/hubs/notifications</c> (§7). Giai đoạn này chỉ khai báo;
/// phần cài đặt bằng SignalR nằm ở <c>Anthill.Infrastructure/Realtime</c>.
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

    /// <summary>Bài sắp đóng. Người nhận: <b>một</b> học sinh chưa nộp, và giáo viên sở hữu đề.</summary>
    /// <remarks>
    /// Mỗi lần gọi là một em, vì từ khi có khung giờ riêng theo lớp thì hai em cùng một đề không còn
    /// chung hạn nộp. Một payload cho cả danh sách là báo giờ sai cho mọi lớp không trùng lịch chung.
    /// </remarks>
    Task DeadlineSoonAsync(
        int studentUserId,
        int teacherUserId,
        DeadlineSoonEvent payload,
        CancellationToken cancellationToken = default);

    /// <summary>Giáo viên sửa đề đã giao (đổi lịch, đổi thời lượng). Người nhận: <b>một</b> học sinh đã được giao.</summary>
    /// <remarks>
    /// Mỗi lần gọi là một em, payload dựng bằng <c>ExamAssignmentMappingExtensions.ToUpdatedEvent</c>
    /// để mang khung giờ thật của em đó. Em có giờ riêng không bị ảnh hưởng khi chỉ lịch chung đổi.
    /// </remarks>
    Task ExamUpdatedAsync(
        int studentUserId,
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

    /// <summary>
    /// Giờ mở CÓ HIỆU LỰC của chính em nhận sự kiện — lịch riêng của lớp nếu có, ngược lại là lịch
    /// chung của đề. Luôn trùng với <c>openAt</c> của <c>GET /api/my/exams</c>.
    /// </summary>
    public required DateTime OpenAt { get; init; }

    /// <summary>Giờ đóng CÓ HIỆU LỰC của chính em nhận sự kiện. Cùng quy tắc với <see cref="OpenAt"/>.</summary>
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

    /// <summary>Bản ghi giao bài của chính em nhận sự kiện — hạn nộp tính theo bản ghi, không theo đề.</summary>
    public required int AssignmentId { get; init; }

    public required string ExamTitle { get; init; }

    /// <summary>
    /// Hạn thật của em này: <c>ExpiresAt</c> của lượt đang làm, hoặc giờ đóng có hiệu lực
    /// (<c>AssignmentSchedule.Resolve</c>) khi em chưa bắt đầu. Không bao giờ là <c>exam.CloseAt</c> trần.
    /// </summary>
    public required DateTime CloseAt { get; init; }

    /// <summary>Số phút còn lại tới <see cref="CloseAt"/> tại thời điểm bắn, tính theo đồng hồ server.</summary>
    public required int MinutesRemaining { get; init; }
}

public sealed record ExamUpdatedEvent
{
    public required int ExamId { get; init; }

    /// <summary>Bản ghi giao bài của chính em nhận sự kiện.</summary>
    public required int AssignmentId { get; init; }

    public required string ExamTitle { get; init; }
    public required int DurationMinutes { get; init; }

    /// <summary>Giờ mở CÓ HIỆU LỰC của em nhận sự kiện — lịch riêng của lớp nếu có, ngược lại lịch chung.</summary>
    public required DateTime OpenAt { get; init; }

    /// <summary>Giờ đóng CÓ HIỆU LỰC của em nhận sự kiện. Cùng quy tắc với <see cref="OpenAt"/>.</summary>
    public required DateTime CloseAt { get; init; }
}
