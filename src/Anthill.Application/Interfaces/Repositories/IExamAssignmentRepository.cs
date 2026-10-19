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

    /// <summary>
    /// Mọi lượt giao bài đã đi qua một lớp (<c>AssignedViaGroupId</c>), kèm đề, người soạn đề và lượt làm bài — để tóm tắt
    /// lịch sử bài kiểm tra của lớp. Không lọc theo lớp hiện tại của em: em đã rời lớp mà vẫn giữ bài đã làm thì vẫn đếm.
    /// </summary>
    Task<IReadOnlyList<ExamAssignment>> GetByGroupAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>Còn lượt giao bài nào đi qua lớp này không (đã làm, đang làm hay hết hạn) — lớp có lịch sử bài thì không xoá được.</summary>
    Task<bool> AnyViaGroupAsync(int groupId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Như trên, nhưng bỏ qua những lượt giao trong <paramref name="excludeAssignmentIds"/> khi tính MAX
    /// (H10). Dùng khi đang xét giao THÊM người mới vào một đề dùng bản đề chung: lượt giao mới đã được
    /// ghi xuống (cần id thật để làm seed nếu là bản đề riêng) nhưng không được tính vào mốc mở đáp án
    /// của những lượt giao CŨ — nếu không, một lớp mới với giờ đóng muộn hơn sẽ đẩy mốc MAX ra tương lai
    /// và che mất việc lớp cũ đã thấy đáp án rồi.
    /// </summary>
    Task<DateTime?> GetAnswersReleaseAtAsync(
        int examId,
        IReadOnlyCollection<int> excludeAssignmentIds,
        CancellationToken cancellationToken = default);

    /// <summary>Các bản ghi của đề mà em chưa bắt đầu làm bài, không theo dõi thay đổi.</summary>
    /// <remarks>
    /// Đúng những em một lần sửa lịch hay thời lượng còn tác động tới — em đã bắt đầu giữ hạn nộp đã chốt.
    /// Gọi trong transaction đang khoá các dòng giao bài để danh sách không lệch với lần sửa.
    /// </remarks>
    Task<IReadOnlyList<ExamAssignment>> GetNotStartedByExamAsync(int examId, CancellationToken cancellationToken = default);

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
    /// Khoá ghi (<c>FOR UPDATE</c>) mọi dòng giao bài của <b>một học sinh</b> rồi trả về chúng cùng đề và lượt
    /// làm bài đọc <b>sau</b> khi đã cầm khoá.
    /// </summary>
    /// <remarks>
    /// Dành cho việc gỡ một em khỏi lớp hay chuyển lớp: phải biết em đã bắt đầu bài nào chưa và thu hồi những
    /// bài chưa bắt đầu mà không bị một lần bấm "Vào thi" chen ngang. Khoá này xung đột với
    /// <see cref="LockForStartAsync"/>, nên hoặc em vào thi trước (lượt của em hiện ra ở phép đọc dưới đây và
    /// bài đó được giữ lại), hoặc em phải chờ lệnh này commit rồi thấy bài đã bị gỡ. Dòng giao bài được khoá
    /// theo thứ tự <c>Id</c>, cùng thứ tự với <see cref="LockByExamAsync"/>. Phải gọi bên trong transaction.
    /// </remarks>
    Task<IReadOnlyList<ExamAssignment>> LockByStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Khoá ghi (<c>FOR UPDATE</c>) một dòng giao bài trước khi tạo lượt làm bài, rồi trả về nó cùng đề
    /// và lượt làm bài đọc <b>sau</b> khi đã cầm khoá.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Khoá này xung đột với <see cref="LockByExamAsync"/> — như trước: một lần sửa đề đang sinh lại bản
    /// đề thì em phải chờ nó commit rồi mới đọc đề và bản đề; ngược lại em cầm khoá trước thì lần sửa phải
    /// chờ lượt của em được ghi xong, và phép đếm "đã có em làm" của nó sẽ thấy lượt đó.
    /// </para>
    /// <para>
    /// <b>Trước đây khoá này là <c>FOR SHARE</c></b>, với lý do hai tab của cùng một em (hay một request
    /// bị gửi trùng — React StrictMode, double-click, retry sau timeout) không chặn nhau vì hai khoá chia
    /// sẻ không loại trừ nhau. Đó chính là lỗ hổng: cả hai request cùng đọc "chưa có lượt nào" trước khi
    /// bên nào kịp ghi, cả hai cùng đi tiếp tới INSERT, và chỉ unique index trên
    /// <c>ExamAttempts.AssignmentId</c> mới chặn được bản ghi thứ hai — bên thua cuộc nhận thẳng một lỗi
    /// 409 thô từ database thay vì được dẫn vào nhánh Resume, dù lượt làm bài của chính em đó đã được tạo
    /// và đồng hồ đã chạy. Đổi sang <c>FOR UPDATE</c> để request thứ hai phải chờ request thứ nhất commit,
    /// rồi tự đọc lại dưới khoá và thấy lượt vừa được tạo — vào đúng nhánh Resume thay vì đua nhau INSERT.
    /// <c>ExamAttemptService.CreateAttemptAsync</c> vẫn bắt riêng vi phạm unique index như một lưới đỡ
    /// thứ hai, phòng trường hợp có một đường ghi khác vào <c>ExamAttempts</c> không đi qua khoá này.
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
    /// Khoá này xung đột với khoá của <see cref="LockForStartAsync"/>, nên em đang bấm "Vào thi" hoặc đã
    /// vào trước và lượt làm bài được thấy ở đây, hoặc phải chờ lần dời giờ commit rồi đọc khung giờ mới.
    /// Không khoá thì lần dời giờ có thể ghi đè khung giờ của một em vừa bắt đầu, và thẻ bài với đồng hồ
    /// của em nói hai giờ khác nhau. Trả NULL khi dòng không còn. Phải gọi bên trong transaction.
    ///
    /// Tiến trình nhắc sắp hết giờ cũng dùng hàm này: nó cần đúng những dữ liệu đó, và cần chính khoá đó để
    /// hai instance cùng quét không gửi hai lời nhắc cho một em.
    /// </remarks>
    Task<ExamAssignment?> LockForRescheduleAsync(int assignmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Những lượt giao có thể tới lúc nhắc "sắp hết giờ": chưa bắt đầu, đề đã xuất bản, tài khoản đang hoạt
    /// động, và giờ đóng có hiệu lực nằm trong khoảng từ bây giờ tới <paramref name="horizon"/> nữa.
    /// </summary>
    /// <remarks>
    /// Lọc thô ở database rồi để <see cref="Common.DeadlineReminder"/> quyết từng dòng: mốc nhắc phụ thuộc
    /// thời lượng của từng đề nên không viết gọn được thành một điều kiện SQL, còn kéo cả bảng về để lọc
    /// trong bộ nhớ thì mỗi phút một lần là quá tốn. Sắp theo giờ đóng gần nhất, để một lượt quét bị trần
    /// chặn lại vẫn xử lý những em gấp nhất trước.
    /// </remarks>
    Task<IReadOnlyList<ExamAssignment>> GetDeadlineReminderCandidatesAsync(
        DateTime utcNow,
        TimeSpan horizon,
        int batchSize,
        CancellationToken cancellationToken = default);

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
