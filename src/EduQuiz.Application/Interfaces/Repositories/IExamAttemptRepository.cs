using EduQuiz.Application.Common;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Interfaces.Repositories;

/// <summary>
/// Lượt làm bài. Mỗi assignment tối đa một attempt (v1 không cho làm lại), nên
/// <see cref="GetByAssignmentAsync"/> vừa là "lấy" vừa là "kiểm tra đã bắt đầu chưa".
/// </summary>
public interface IExamAttemptRepository : IGenericRepository<ExamAttempt>
{
    Task<ExamAttempt?> GetByAssignmentAsync(int assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Nạp kèm assignment, exam và bản đề — đủ để dựng lại phiên làm bài sau F5.</summary>
    Task<ExamAttempt?> GetSessionAsync(int attemptId, CancellationToken cancellationToken = default);

    /// <summary>Nạp kèm đáp án đã chọn, dùng lúc chấm và lúc xem chi tiết kết quả.</summary>
    Task<ExamAttempt?> GetWithAnswersAsync(int attemptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chủ sở hữu của một lượt làm bài. Trả NULL khi attempt không tồn tại — service phân biệt
    /// "không có" với "không phải của mình" rồi trả 404 cho cả hai (§4.2).
    /// </summary>
    Task<int?> GetOwnerStudentIdAsync(int attemptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nạp một lượt làm bài kèm <b>khoá ghi ở mức dòng</b>, giữ tới khi transaction kết thúc.
    /// </summary>
    /// <remarks>
    /// Chỉ có nghĩa khi gọi bên trong một transaction. Lưu nháp, nộp bài và chốt sổ hết giờ đều là
    /// chuỗi "đọc trạng thái — quyết định — ghi", và cả ba có thể chồng lên nhau: autosave 800ms một
    /// nhịp, retry sau timeout mạng, hai tab cùng mở. Không khoá thì hai request cùng đọc được
    /// trạng thái cũ, cùng đi qua phép kiểm rồi cùng ghi — §5 nói thẳng là chỉ khoá ở database mới chắc.
    /// </remarks>
    Task<ExamAttempt?> LockForUpdateAsync(int attemptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bài <c>InProgress</c> đã quá <c>ExpiresAt</c>. Background job dùng để tự chấm, vì học sinh
    /// tắt trình duyệt thì không ai gọi submit cả (§3 quy tắc ③).
    /// </summary>
    Task<IReadOnlyList<ExamAttempt>> GetExpiredInProgressAsync(
        DateTime utcNow,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>Bài sắp hết hạn và chưa nộp, cho sự kiện realtime <c>DeadlineSoon</c> (§7).</summary>
    Task<IReadOnlyList<ExamAttempt>> GetDeadlineSoonAsync(
        DateTime utcNow,
        TimeSpan window,
        CancellationToken cancellationToken = default);

    /// <summary>Bảng kết quả của giáo viên, lọc chéo theo đề / học sinh / nhóm / trạng thái (§4.8).</summary>
    Task<PagedResult<ExamAttempt>> GetResultsPagedAsync(
        int teacherId,
        string? search,
        int? examId,
        int? studentId,
        int? groupId,
        AssignmentStatus? status,
        PaginationParams pagination,
        CancellationToken cancellationToken = default);

    /// <summary>Lịch sử làm bài của chính học sinh đang đăng nhập.</summary>
    Task<PagedResult<ExamAttempt>> GetMyResultsPagedAsync(
        int studentId,
        string? search,
        PaginationParams pagination,
        CancellationToken cancellationToken = default);

    /// <summary><c>Student.completedExams</c> — số lượt ở trạng thái submitted hoặc graded (§5).</summary>
    Task<int> CountCompletedByStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary><c>Student.averageScore</c> — trung bình điểm trên các lượt đã chấm, làm tròn 1 chữ số (§5).</summary>
    Task<decimal> GetAverageScoreByStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Hai con số dẫn xuất cho nhiều học sinh cùng lúc, tránh N+1 ở màn hình danh sách học sinh.</summary>
    Task<IReadOnlyDictionary<int, (int Completed, decimal AverageScore)>> GetStatsByStudentsAsync(
        IEnumerable<int> studentIds,
        CancellationToken cancellationToken = default);

    Task<int> CountSubmittedSinceAsync(int teacherId, DateTime sinceUtc, CancellationToken cancellationToken = default);
}
