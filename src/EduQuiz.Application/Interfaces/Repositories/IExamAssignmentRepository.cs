using EduQuiz.Domain.Entities;

namespace EduQuiz.Application.Interfaces.Repositories;

/// <summary>
/// Việc giao đề cho học sinh. Duy nhất theo cặp <c>(ExamId, StudentId)</c> — thao tác giao bài
/// phải idempotent, giao lại cho người đã có bài thì bỏ qua chứ không tạo bản ghi thứ hai (§5).
/// </summary>
public interface IExamAssignmentRepository : IGenericRepository<ExamAssignment>
{
    /// <summary>Danh sách đã giao của một đề, kèm hồ sơ học sinh và lượt làm bài để dựng bảng theo dõi.</summary>
    Task<IReadOnlyList<ExamAssignment>> GetByExamAsync(int examId, CancellationToken cancellationToken = default);

    Task<ExamAssignment?> GetByExamAndStudentAsync(int examId, int studentId, CancellationToken cancellationToken = default);

    /// <summary>Nạp kèm Exam và Attempt — mọi kiểm tra quyền và hạn giờ đều cần cả ba.</summary>
    Task<ExamAssignment?> GetDetailAsync(int assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Học sinh nào trong tập này đã được giao rồi — để khử trùng lặp trước khi ghi.</summary>
    Task<IReadOnlyList<int>> GetAssignedStudentIdsAsync(
        int examId,
        IEnumerable<int> studentIds,
        CancellationToken cancellationToken = default);

    /// <summary>Bài được giao cho chính học sinh đang đăng nhập. Danh tính suy từ JWT (§3 quy tắc ④).</summary>
    Task<IReadOnlyList<ExamAssignment>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra quyền sở hữu trước khi làm bất cứ việc gì trên assignment (§3 quy tắc ④).</summary>
    Task<bool> IsOwnedByStudentAsync(int assignmentId, int studentId, CancellationToken cancellationToken = default);

    /// <summary><c>Exam.assignedCount</c> — dữ liệu dẫn xuất, BE tính chứ không nhận từ client (§5).</summary>
    Task<int> CountByExamAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary><c>Exam.submittedCount</c> — đếm assignment có attempt ở trạng thái submitted hoặc graded.</summary>
    Task<int> CountSubmittedByExamAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Hai con số dẫn xuất cho nhiều đề cùng lúc, tránh N+1 ở màn hình danh sách đề.</summary>
    Task<IReadOnlyDictionary<int, (int Assigned, int Submitted)>> CountByExamsAsync(
        IEnumerable<int> examIds,
        CancellationToken cancellationToken = default);

    Task RemoveByExamAsync(int examId, CancellationToken cancellationToken = default);
}
