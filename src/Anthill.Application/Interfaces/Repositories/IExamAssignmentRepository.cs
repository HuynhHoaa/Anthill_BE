using Anthill.Application.Common;
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

    /// <summary>
    /// Khoá chia sẻ (<c>FOR SHARE</c>) một dòng giao bài trước khi tạo lượt làm bài, rồi trả về nó cùng đề
    /// và lượt làm bài đọc <b>sau</b> khi đã cầm khoá.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Khoá này xung đột với <see cref="LockByExamAsync"/>: một lần sửa đề đang sinh lại bản đề thì em
    /// phải chờ nó commit rồi mới đọc đề và bản đề; ngược lại em cầm khoá trước thì lần sửa phải chờ lượt
    /// của em được ghi xong, và phép đếm "đã có em làm" của nó sẽ thấy lượt đó. Hai em khác nhau, hay hai
    /// tab của cùng một em, không chặn nhau vì hai khoá chia sẻ không loại trừ nhau.
    /// </para>
    /// <para>
    /// Đề, dòng giao bài và lượt làm bài (nếu đã có) đều được đọc lại, kể cả khi chúng đã nằm trong change
    /// tracker từ lần đọc kiểm quyền: EF giữ nguyên giá trị của lần đọc trước cho thực thể đã theo dõi,
    /// tức là ảnh chụp từ trước lúc phải chờ khoá. Bản đề riêng đã nạp kèm cũng được đọc lại — nếu lần sửa
    /// vừa xoá nó thì nó rời change tracker, không còn đứng chắn bản đề mới của cùng lượt giao.
    /// </para>
    /// <para>Trả NULL khi dòng giao bài đã bị thu hồi trong lúc chờ. Phải gọi bên trong transaction.</para>
    /// </remarks>
    Task<ExamAssignment?> LockForStartAsync(int assignmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Khoá ghi (<c>FOR UPDATE</c>) một dòng giao bài để dời giờ, rồi nạp đề, hồ sơ học sinh kèm lớp, lớp đã
    /// giao qua và lượt làm bài — tất cả đọc <b>sau</b> khi đã cầm khoá.
    /// </summary>
    /// <remarks>
    /// Khoá này xung đột với khoá chia sẻ của <see cref="LockForStartAsync"/>, nên em đang bấm "Vào thi"
    /// hoặc đã vào trước và lượt làm bài được thấy ở đây, hoặc phải chờ lần dời giờ commit rồi đọc khung
    /// giờ mới. Không khoá thì lần dời giờ có thể ghi đè khung giờ của một em vừa bắt đầu, và thẻ bài với
    /// đồng hồ của em nói hai giờ khác nhau. Trả NULL khi dòng không còn. Phải gọi bên trong transaction.
    /// </remarks>
    Task<ExamAssignment?> LockForRescheduleAsync(int assignmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bản ghi giao bài của nhiều đề, gom theo (đề, lớp đã giao qua, cặp giờ thô, đã bắt đầu hay chưa) — nguyên
    /// liệu cho <c>AssignmentWindowSummary</c>.
    /// </summary>
    /// <remarks>Gom trong SQL: màn hình danh sách chín đề, mỗi đề vài trăm em, chỉ kéo về vài chục dòng.</remarks>
    Task<IReadOnlyList<AssignmentWindowRow>> GetWindowRowsByExamsAsync(
        IEnumerable<int> examIds,
        CancellationToken cancellationToken = default);

    /// <summary>Ba con số dẫn xuất cho nhiều đề cùng lúc, tránh N+1 ở màn hình danh sách đề.</summary>
    Task<IReadOnlyDictionary<int, (int Assigned, int Submitted, int Started)>> CountByExamsAsync(
        IEnumerable<int> examIds,
        CancellationToken cancellationToken = default);

    Task RemoveByExamAsync(int examId, CancellationToken cancellationToken = default);
}
