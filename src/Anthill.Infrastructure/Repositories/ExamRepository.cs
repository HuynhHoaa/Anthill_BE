using Anthill.Application.Common;
using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

public class ExamRepository : GenericRepository<Exam>, IExamRepository
{
    public ExamRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<Exam>> GetPagedAsync(
        int? createdByUserId,
        string? search,
        ExamLifecycle? lifecycle,
        DateTime asOfUtc,
        PaginationParams pagination,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Include(e => e.CreatedBy)
            .AsQueryable();

        if (createdByUserId.HasValue)
        {
            query = query.Where(e => e.CreatedByUserId == createdByUserId.Value);
        }

        if (SearchText.ToLikePattern(search) is { } pattern)
        {
            query = query.Where(e =>
                EF.Functions.ILike(EF.Functions.Unaccent(e.Title), pattern)
                || EF.Functions.ILike(EF.Functions.Unaccent(e.Description ?? string.Empty), pattern)
                || EF.Functions.ILike(EF.Functions.Unaccent(e.Topic ?? string.Empty), pattern));
        }

        query = ApplyLifecycleFilter(query, lifecycle, asOfUtc);

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .ToPagedResultAsync(pagination, cancellationToken);
    }

    public async Task<IReadOnlyList<Exam>> GetRecentAsync(
        int createdByUserId,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return [];
        }

        return await DbSet
            .AsNoTracking()
            .Where(e => e.CreatedByUserId == createdByUserId)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsOwnedByAsync(int examId, int teacherId, CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(e => e.Id == examId && e.CreatedByUserId == teacherId, cancellationToken);

    /// <inheritdoc />
    public async Task<Exam?> LockForEditAsync(int examId, CancellationToken cancellationToken = default)
    {
        // Phải hỏi trước khi truy vấn, vì ngay sau nó thì dòng nào cũng đã nằm trong change tracker.
        var trackedBefore = Context.ChangeTracker
            .Entries<Exam>()
            .Any(entry => entry.Entity.Id == examId);

        // Không nối thêm toán tử LINQ nào phía sau, để EF gửi thẳng câu lệnh này xuống thay vì bọc nó vào
        // một truy vấn con.
        var rows = await DbSet
            .FromSql($"""SELECT * FROM "Exams" WHERE "Id" = {examId} FOR NO KEY UPDATE""")
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var exam = rows[0];

        if (trackedBefore)
        {
            // Đề đã được đọc lúc kiểm quyền, trước khi xếp hàng chờ khoá. EF trả lại chính đối tượng đó với
            // giá trị cũ, mà đem giá trị cũ ra so với request là quyết định trên một cấu hình người khác vừa
            // sửa xong. Đọc lại ở đây an toàn vì dòng đã bị khoá.
            await Context.Entry(exam).ReloadAsync(cancellationToken);
        }

        return exam;
    }

    public async Task<ExamPaper?> GetSharedPaperAsync(int examId, CancellationToken cancellationToken = default) =>
        await Context.ExamPapers
            .FirstOrDefaultAsync(p => p.ExamId == examId && p.AssignmentId == null, cancellationToken);

    public async Task<ExamPaper?> GetPaperByAssignmentAsync(
        int assignmentId,
        CancellationToken cancellationToken = default) =>
        await Context.ExamPapers
            .FirstOrDefaultAsync(p => p.AssignmentId == assignmentId, cancellationToken);

    /// <summary>
    /// Thứ tự câu và thứ tự đáp án đọc thẳng từ cột <c>Ordinal</c> đã chốt lúc sinh đề.
    /// Không xáo lại ở tầng này — §5 nói rõ việc xáo nằm trong bước sinh đề, không phải bước trả về.
    /// </summary>
    public async Task<ExamPaper?> GetPaperWithQuestionsAsync(
        int examPaperId,
        CancellationToken cancellationToken = default) =>
        await Context.ExamPapers
            .Include(p => p.Questions.OrderBy(q => q.Ordinal))
                .ThenInclude(q => q.Answers.OrderBy(a => a.Ordinal))
            .FirstOrDefaultAsync(p => p.Id == examPaperId, cancellationToken);

    public async Task AddPaperAsync(ExamPaper paper, CancellationToken cancellationToken = default) =>
        await Context.ExamPapers.AddAsync(paper, cancellationToken);

    public async Task RemovePapersAsync(int examId, CancellationToken cancellationToken = default)
    {
        var papers = await Context.ExamPapers
            .Where(p => p.ExamId == examId)
            .ToListAsync(cancellationToken);

        // Câu hỏi và đáp án của bản đề đi theo nhờ ON DELETE CASCADE ở database.
        Context.ExamPapers.RemoveRange(papers);
    }

    public async Task<int> CountAssignedByCreatorAsync(
        int createdByUserId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .CountAsync(
                exam => exam.CreatedByUserId == createdByUserId && exam.Assignments.Any(),
                cancellationToken);

    public async Task<int> CountByCreatorAsync(int createdByUserId, CancellationToken cancellationToken = default) =>
        await DbSet.CountAsync(e => e.CreatedByUserId == createdByUserId, cancellationToken);

    /// <summary>
    /// <c>lifecycle</c> không phải cột lưu cứng (§5): database chỉ giữ <c>IsDraft</c>, ba trạng thái
    /// còn lại là phép so <c>OpenAt</c>/<c>CloseAt</c> với mốc thời gian truyền vào. Nhận
    /// <paramref name="asOfUtc"/> thay vì đọc <c>DateTime.UtcNow</c> tại chỗ để một lần đọc danh sách
    /// dùng đúng một mốc — bộ đếm và bộ lọc không được lệch nhau vì trôi mất vài mili giây.
    /// </summary>
    private static IQueryable<Exam> ApplyLifecycleFilter(
        IQueryable<Exam> query,
        ExamLifecycle? lifecycle,
        DateTime asOfUtc) => lifecycle switch
    {
        ExamLifecycle.Draft => query.Where(e => e.IsDraft),
        ExamLifecycle.Scheduled => query.Where(e => !e.IsDraft && asOfUtc < e.OpenAt),
        ExamLifecycle.Open => query.Where(e => !e.IsDraft && e.OpenAt <= asOfUtc && asOfUtc <= e.CloseAt),
        ExamLifecycle.Closed => query.Where(e => !e.IsDraft && asOfUtc > e.CloseAt),
        _ => query
    };
}
