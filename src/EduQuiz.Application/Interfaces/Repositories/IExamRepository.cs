using EduQuiz.Application.Common;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Interfaces.Repositories;

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
