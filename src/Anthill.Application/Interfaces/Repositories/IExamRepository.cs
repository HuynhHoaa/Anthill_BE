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

    /// <summary>Đề riêng của một học sinh, có khi bật randomizePerStudent.</summary>
    Task<ExamPaper?> GetPaperByAssignmentAsync(int assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Nạp trọn một bản đề: câu hỏi theo Ordinal, mỗi câu kèm đáp án theo Ordinal.</summary>
    Task<ExamPaper?> GetPaperWithQuestionsAsync(int examPaperId, CancellationToken cancellationToken = default);

    Task AddPaperAsync(ExamPaper paper, CancellationToken cancellationToken = default);

    /// <summary>Xoá đề kéo theo mọi bản đề đã sinh — nằm trong cùng transaction với xoá assignment (§5).</summary>
    Task RemovePapersAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Số đề đã giao cho ít nhất một học sinh — khác với tổng số đề đã tạo.</summary>
    /// <remarks>
    /// Hai con số này tách nhau vì chúng trả lời hai câu khác nhau: "tôi đã soạn bao nhiêu đề" và
    /// "bao nhiêu đề trong số đó thực sự đến tay học sinh". Một giáo viên có mười đề nháp và một
    /// đề đã giao thì hai ô số liệu phải nói được điều đó.
    /// </remarks>
    Task<int> CountAssignedByCreatorAsync(int createdByUserId, CancellationToken cancellationToken = default);

    Task<int> CountByCreatorAsync(int createdByUserId, CancellationToken cancellationToken = default);
}
