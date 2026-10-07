using Anthill.Application.Common;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Interfaces.Repositories;

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

    /// <summary>Lịch sử làm bài của chính học sinh đang đăng nhập, chỉ gồm lượt đã nộp hoặc đã chấm.</summary>
    /// <remarks>
    /// Lượt đang làm dở không phải kết quả: nó chưa có điểm, chưa có giờ nộp, và là việc em cần quay
    /// lại làm chứ không phải thứ để xem lại. Nó nằm ở danh sách bài kiểm tra của em.
    /// </remarks>
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

    /// <summary>
    /// Bốn ô số liệu đầu trang Kết quả, tính bằng <b>một</b> truy vấn gộp trên cùng bộ lọc với bảng.
    /// </summary>
    /// <remarks>
    /// Gộp vào một lượt đi database thay vì đếm từng chỉ số: bốn con số này luôn hiện cùng nhau,
    /// và tính rời thì chúng có thể đọc ở bốn thời điểm khác nhau — đủ để tổng không khớp với bảng
    /// ngay bên dưới khi có người vừa nộp bài.
    ///
    /// <c>AssignedCount</c> đếm trên bảng assignment chứ không trên bảng attempt, vì tỷ lệ hoàn
    /// thành phải lấy mẫu số là số bài đã giao — em chưa từng vào thi thì không có dòng attempt
    /// nào, mà chính những em đó mới là phần còn thiếu của tỷ lệ.
    /// </remarks>
    Task<ResultStatsSnapshot> GetResultStatsAsync(
        int teacherId,
        string? search,
        int? examId,
        int? studentId,
        int? groupId,
        AssignmentStatus? status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Số câu của bản đề ứng với từng lượt làm bài, lấy cho nhiều lượt cùng lúc.
    /// </summary>
    /// <remarks>
    /// Đọc từ bản đề chứ không từ <c>Exam.TotalQuestions</c>: cấu hình của đề sửa được sau khi đã
    /// giao, còn bản đề trong tay học sinh thì không đổi. Hai nguồn này lệch nhau là bảng kết quả
    /// hiện "18/40 câu đúng" cho một bài vốn chỉ có 30 câu.
    /// </remarks>
    Task<IReadOnlyDictionary<int, int>> GetPaperQuestionCountsAsync(
        IEnumerable<int> attemptIds,
        CancellationToken cancellationToken = default);
    Task<int> CountSubmittedSinceAsync(int teacherId, DateTime sinceUtc, CancellationToken cancellationToken = default);
}

/// <summary>Số liệu thô của bảng kết quả, chưa qua bước quyết định NULL hay 0.</summary>
/// <remarks>
/// Repository trả về sự thật trần trụi — đã chấm bao nhiêu bài, trung bình bao nhiêu — còn việc
/// "chưa chấm bài nào thì hiện gì" là quyết định của tầng Application, nơi biết màn hình cần gì.
/// </remarks>
public sealed record ResultStatsSnapshot
{
    public required int TotalAttempts { get; init; }

    public required int GradedCount { get; init; }

    public required int SubmittedCount { get; init; }

    /// <summary>Số lượt đang làm dở — chưa nộp, chưa có điểm, không thuộc <see cref="SubmittedCount"/>.</summary>
    public required int InProgressCount { get; init; }

    /// <summary>Tổng số bài đã giao khớp bộ lọc — mẫu số của tỷ lệ hoàn thành.</summary>
    public required int AssignedCount { get; init; }

    /// <summary>Chỉ có nghĩa khi <see cref="GradedCount"/> lớn hơn 0.</summary>
    public required decimal AverageScore { get; init; }

    /// <summary>Chỉ có nghĩa khi <see cref="GradedCount"/> lớn hơn 0.</summary>
    public required decimal HighestScore { get; init; }
}
