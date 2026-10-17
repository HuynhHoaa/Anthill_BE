using Anthill.Application.Common;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Interfaces.Repositories;

/// <summary>
/// Đề kiểm tra và các bản đề đã sinh (<see cref="ExamPaper"/>).
/// <c>lifecycle</c> không phải cột lưu cứng: chỉ <c>IsDraft</c>/<c>PublishedAt</c> nằm trong DB,
/// ba trạng thái còn lại suy từ <c>OpenAt</c>/<c>CloseAt</c> so với giờ server (§5) — nên
/// bộ lọc vòng đời phải nhận <paramref name="asOfUtc"/> chứ không đọc <c>DateTime.UtcNow</c> rải rác.
/// </summary>
public interface IExamRepository : IGenericRepository<Exam>
{
    Task<PagedResult<Exam>> GetPagedAsync(
        int? createdByUserId,
        string? search,
        ExamLifecycle? lifecycle,
        DateTime asOfUtc,
        PaginationParams pagination,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Exam>> GetRecentAsync(
        int createdByUserId,
        int count,
        CancellationToken cancellationToken = default);

    Task<bool> IsOwnedByAsync(int examId, int teacherId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Khoá ghi dòng đề (<c>FOR NO KEY UPDATE</c>) rồi trả về đề với giá trị đọc <b>sau</b> khi đã cầm khoá.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dành cho hai thao tác đọc cấu hình đề rồi quyết định ghi: sửa đề và giao bài. Hai lần sửa, hoặc một
    /// lần sửa và một lần giao, trên cùng một đề phải xếp hàng: không khoá thì lần giao có thể sinh bản đề
    /// từ cấu hình cũ đúng lúc lần sửa vừa xoá và sinh lại bản đề theo cấu hình mới.
    /// </para>
    /// <para>
    /// <c>FOR NO KEY UPDATE</c> chứ không <c>FOR UPDATE</c>: thêm một bản đề cần khoá chia sẻ khoá ngoại
    /// (<c>FOR KEY SHARE</c>) trên dòng đề, và <c>FOR UPDATE</c> chặn cả khoá đó. Một em đang giữ khoá dòng
    /// giao bài để vào thi mà cần sinh bản đề sẽ phải chờ lần sửa, trong khi lần sửa lại chờ chính dòng giao
    /// bài em đang giữ — hai bên chờ nhau mãi. Hai lần khoá <c>FOR NO KEY UPDATE</c> vẫn loại trừ nhau.
    /// </para>
    /// <para>
    /// Trả NULL khi đề đã bị xoá trong lúc chờ. Phải gọi bên trong transaction.
    /// </para>
    /// </remarks>
    Task<Exam?> LockForEditAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Đề chung của cả lớp (<c>AssignmentId = NULL</c>), chỉ có khi tắt randomizePerStudent.</summary>
    Task<ExamPaper?> GetSharedPaperAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mã các câu hỏi mà học sinh của những lớp này đã <b>nhận</b> trong các bản đề đã sinh khi giao bài qua lớp: bản riêng của từng
    /// em, cộng bản chung của các đề giao cho lớp. Đề lớn chia nhiều đề chỉ tính các câu thực sự rơi vào đề của lớp, không tính
    /// những câu đề lớn có mà lớp không nhận.
    /// </summary>
    Task<IReadOnlyCollection<int>> GetQuestionIdsDeliveredToGroupsAsync(
        IReadOnlyCollection<int> groupIds,
        CancellationToken cancellationToken = default);

    /// <summary>Đề riêng của một học sinh, có khi bật randomizePerStudent.</summary>
    Task<ExamPaper?> GetPaperByAssignmentAsync(int assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Nạp trọn một bản đề: câu hỏi theo Ordinal, mỗi câu kèm đáp án theo Ordinal.</summary>
    Task<ExamPaper?> GetPaperWithQuestionsAsync(int examPaperId, CancellationToken cancellationToken = default);

    Task AddPaperAsync(ExamPaper paper, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xoá đề kéo theo mọi bản đề đã sinh — nằm trong cùng transaction với xoá assignment (§5). Cũng xoá cách chia đề nhỏ
    /// của đề (<see cref="GetVariantsAsync"/>), để lần giao sau chia lại theo cấu hình mới.
    /// </summary>
    Task RemovePapersAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Bộ đề nhỏ đã chia cho một lớp trên một đề được chia thành nhiều đề; rỗng khi chưa chia. Theo dõi thay đổi.</summary>
    Task<IReadOnlyList<ExamPaperVariant>> GetVariantsAsync(
        int examId,
        int? groupId,
        CancellationToken cancellationToken = default);

    /// <summary>Mọi đề nhỏ của đề, kèm tên lớp, không theo dõi thay đổi — cho màn hình xem cách chia.</summary>
    Task<IReadOnlyList<ExamPaperVariant>> GetAllVariantsAsync(int examId, CancellationToken cancellationToken = default);

    Task AddVariantsAsync(IEnumerable<ExamPaperVariant> variants, CancellationToken cancellationToken = default);

    /// <summary>Số em đang cầm từng đề nhỏ của một lớp (khoá là số thứ tự đề); đề chưa em nào cầm không có mặt.</summary>
    Task<IReadOnlyDictionary<int, int>> GetVariantUsageAsync(
        int examId,
        int? groupId,
        CancellationToken cancellationToken = default);

    /// <summary>Như trên cho mọi lớp của đề: (lớp, số thứ tự đề, số em).</summary>
    Task<IReadOnlyList<(int? GroupId, int VariantIndex, int Students)>> GetVariantUsageByExamAsync(
        int examId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Id các đề mà bản đề ĐÃ SINH hiện có chứa câu hỏi này (H9) — để <c>QuestionService</c> biết cần
    /// xét sinh lại bản đề của những đề nào sau khi câu hỏi vừa đổi nội dung hoặc bị ẩn.
    /// </summary>
    /// <remarks>Không phân biệt bản đề chung hay bản đề riêng — cả hai đều chứa <c>ExamPaperQuestion</c>.</remarks>
    Task<IReadOnlyList<int>> GetExamIdsUsingQuestionAsync(
        int questionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Các dòng danh sách câu hỏi của đề, theo thứ tự, <b>đang được theo dõi</b> — để xoá và thay bằng danh
    /// sách mới khi giáo viên lưu. Rỗng với đề theo ma trận.
    /// </summary>
    Task<IReadOnlyList<ExamQuestion>> GetQuestionRowsAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Danh sách câu hỏi của đề kèm câu hỏi đầy đủ (đáp án, người soạn), theo thứ tự, không theo dõi thay đổi.
    /// Dùng cho màn hình duyệt đề và cho bộ sinh đề.
    /// </summary>
    Task<IReadOnlyList<ExamQuestion>> GetQuestionListAsync(int examId, CancellationToken cancellationToken = default);

    void RemoveQuestionRows(IEnumerable<ExamQuestion> rows);

    Task AddQuestionRowsAsync(IEnumerable<ExamQuestion> rows, CancellationToken cancellationToken = default);

    /// <summary>Số đề đã giao cho ít nhất một học sinh — khác với tổng số đề đã tạo.</summary>
    /// <remarks>
    /// Hai con số này tách nhau vì chúng trả lời hai câu khác nhau: "tôi đã soạn bao nhiêu đề" và
    /// "bao nhiêu đề trong số đó thực sự đến tay học sinh". Một giáo viên có mười đề nháp và một
    /// đề đã giao thì hai ô số liệu phải nói được điều đó.
    /// </remarks>
    Task<int> CountAssignedByCreatorAsync(int createdByUserId, CancellationToken cancellationToken = default);

    Task<int> CountByCreatorAsync(int createdByUserId, CancellationToken cancellationToken = default);
}
