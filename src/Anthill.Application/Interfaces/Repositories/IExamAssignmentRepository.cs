using Anthill.Domain.Entities;

namespace Anthill.Application.Interfaces.Repositories;

/// <summary>
/// Việc giao đề cho học sinh. Duy nhất theo cặp <c>(ExamId, StudentId)</c> — thao tác giao bài
/// phải idempotent, giao lại cho người đã có bài thì bỏ qua chứ không tạo bản ghi thứ hai (§5).
/// </summary>
public interface IExamAssignmentRepository : IGenericRepository<ExamAssignment>
{
    /// <summary>
    /// Danh sách đã giao của một đề, kèm đề, hồ sơ học sinh và lượt làm bài để dựng bảng theo dõi —
    /// đề cần cho việc suy trạng thái expired theo khung giờ của từng em.
    /// </summary>
    Task<IReadOnlyList<ExamAssignment>> GetByExamAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Nạp kèm Exam và Attempt.</summary>
    Task<ExamAssignment?> GetByExamAndStudentAsync(int examId, int studentId, CancellationToken cancellationToken = default);

    /// <summary>Nạp kèm Exam (và người tạo), Student, Attempt và bản đề riêng — mọi kiểm tra quyền và hạn giờ đều cần.</summary>
    Task<ExamAssignment?> GetDetailAsync(int assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Các bản ghi của đề này đã được giao QUA những lớp đã cho, kèm lượt làm bài — để sửa khung giờ.</summary>
    /// <remarks>
    /// Khớp theo <c>AssignedViaGroupId</c> chứ không theo thành viên hiện tại: em đã chuyển đi vẫn mang
    /// khung giờ của lớp đã giao bài cho em, còn em mới chuyển tới thì chưa hề được giao qua lớp này.
    /// Có theo dõi thay đổi vì service sửa trực tiếp trên các bản ghi này.
    /// </remarks>
    Task<IReadOnlyList<ExamAssignment>> GetByExamViaGroupsAsync(
        int examId,
        IEnumerable<int> groupIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Các khung giờ riêng của một đề mà chưa ai bắt đầu làm — để chặn tăng thời lượng vượt khung.
    /// </summary>
    /// <remarks>
    /// Bỏ qua bản ghi đã có lượt làm bài: hạn nộp của các em đã chốt vào ExpiresAt lúc bắt đầu, nên
    /// thời lượng mới không còn tác động gì tới họ.
    /// </remarks>
    Task<IReadOnlyList<(DateTime OpenAt, DateTime CloseAt)>> GetPendingOverrideWindowsAsync(
        int examId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mốc sớm nhất mà đáp án từng câu của đề được phép rời server: giờ đóng có hiệu lực muộn nhất
    /// trong mọi lượt giao của đề, hoặc hạn nộp muộn nhất của lượt còn đang làm nếu muộn hơn. NULL khi
    /// đề chưa giao cho ai.
    /// </summary>
    /// <remarks>Xem <c>ResultMappingExtensions.ResolveAnswerVisibility</c>.</remarks>
    Task<DateTime?> GetAnswersReleaseAtAsync(int examId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// <c>Exam.startedCount</c> — số assignment đã có lượt làm bài, ở bất kỳ trạng thái nào. Khác 0 là
    /// cấu hình đề bị khoá (<c>ExamEditPolicy</c>).
    /// </summary>
    Task<int> CountStartedByExamAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Khoá ghi (<c>FOR UPDATE</c>) mọi bản ghi giao bài của đề và trả về chúng, có theo dõi thay đổi.
    /// </summary>
    /// <remarks>
    /// Dành cho lần sửa đề có thể sinh lại bản đề. Tạo lượt làm bài phải khoá chia sẻ dòng giao bài mà
    /// nó trỏ tới (khoá ngoại), nên trong lúc khoá này còn giữ không em nào bắt đầu được: phép đếm
    /// <see cref="CountStartedByExamAsync"/> ngay sau đó là con số thật, không phải con số của một
    /// khoảnh khắc đã qua. Phải gọi bên trong transaction.
    /// </remarks>
    Task<IReadOnlyList<ExamAssignment>> LockByExamAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Ba con số dẫn xuất cho nhiều đề cùng lúc, tránh N+1 ở màn hình danh sách đề.</summary>
    Task<IReadOnlyDictionary<int, (int Assigned, int Submitted, int Started)>> CountByExamsAsync(
        IEnumerable<int> examIds,
        CancellationToken cancellationToken = default);

    Task RemoveByExamAsync(int examId, CancellationToken cancellationToken = default);
}
