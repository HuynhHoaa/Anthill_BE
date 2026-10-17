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

    public async Task<IReadOnlyCollection<int>> GetQuestionIdsDeliveredToGroupsAsync(
        IReadOnlyCollection<int> groupIds,
        CancellationToken cancellationToken = default)
    {
        if (groupIds.Count == 0)
        {
            return [];
        }

        var viaGroups = Context.ExamAssignments
            .AsNoTracking()
            .Where(assignment => assignment.AssignedViaGroupId != null && groupIds.Contains(assignment.AssignedViaGroupId.Value));

        // Bản đề riêng của từng em được giao qua các lớp này.
        var ownPapers = Context.ExamPaperQuestions
            .AsNoTracking()
            .Where(row => row.ExamPaper.AssignmentId != null
                && viaGroups.Any(assignment => assignment.Id == row.ExamPaper.AssignmentId))
            .Select(row => row.QuestionId);

        // Bản đề chung (không gắn lượt giao nào) của các đề giao cho lớp: mọi em của lớp đều nhận bản đó.
        var sharedPapers = Context.ExamPaperQuestions
            .AsNoTracking()
            .Where(row => row.ExamPaper.AssignmentId == null
                && viaGroups.Any(assignment => assignment.ExamId == row.ExamPaper.ExamId))
            .Select(row => row.QuestionId);

        return await ownPapers.Union(sharedPapers).ToListAsync(cancellationToken);
    }

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

        // Cách chia đề nhỏ gắn với cấu hình đề đã sinh bản đề: bỏ bản đề thì bỏ luôn cách chia, không để lần giao sau
        // phát lại những đề nhỏ của cấu hình cũ.
        var variants = await Context.ExamPaperVariants
            .Where(v => v.ExamId == examId)
            .ToListAsync(cancellationToken);

        Context.ExamPaperVariants.RemoveRange(variants);
    }

    public async Task<IReadOnlyList<ExamPaperVariant>> GetVariantsAsync(
        int examId,
        int? groupId,
        CancellationToken cancellationToken = default) =>
        await Context.ExamPaperVariants
            .Where(v => v.ExamId == examId && v.GroupId == groupId)
            .OrderBy(v => v.VariantIndex)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExamPaperVariant>> GetAllVariantsAsync(
        int examId,
        CancellationToken cancellationToken = default) =>
        await Context.ExamPaperVariants
            .AsNoTracking()
            .Include(v => v.Group)
            .Where(v => v.ExamId == examId)
            .OrderBy(v => v.GroupId)
            .ThenBy(v => v.VariantIndex)
            .ToListAsync(cancellationToken);

    public async Task AddVariantsAsync(
        IEnumerable<ExamPaperVariant> variants,
        CancellationToken cancellationToken = default) =>
        await Context.ExamPaperVariants.AddRangeAsync(variants, cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> GetVariantUsageAsync(
        int examId,
        int? groupId,
        CancellationToken cancellationToken = default) =>
        await Context.ExamPapers
            .AsNoTracking()
            .Where(p => p.ExamId == examId
                        && p.VariantIndex != null
                        && p.Assignment!.AssignedViaGroupId == groupId)
            .GroupBy(p => p.VariantIndex!.Value)
            .Select(group => new { Variant = group.Key, Students = group.Count() })
            .ToDictionaryAsync(row => row.Variant, row => row.Students, cancellationToken);

    public async Task<IReadOnlyList<(int? GroupId, int VariantIndex, int Students)>> GetVariantUsageByExamAsync(
        int examId,
        CancellationToken cancellationToken = default)
    {
        var rows = await Context.ExamPapers
            .AsNoTracking()
            .Where(p => p.ExamId == examId && p.VariantIndex != null)
            .GroupBy(p => new { p.Assignment!.AssignedViaGroupId, Variant = p.VariantIndex!.Value })
            .Select(group => new { group.Key.AssignedViaGroupId, group.Key.Variant, Students = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.Select(row => (row.AssignedViaGroupId, row.Variant, row.Students)).ToList();
    }

    public async Task<IReadOnlyList<int>> GetExamIdsUsingQuestionAsync(
        int questionId,
        CancellationToken cancellationToken = default)
    {
        var fromPapers = await Context.ExamPaperQuestions
            .Where(paperQuestion => paperQuestion.QuestionId == questionId)
            .Select(paperQuestion => paperQuestion.ExamPaper.ExamId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Đề theo danh sách nắm câu hỏi này ngay từ lúc soạn, trước cả khi có bản đề nào được sinh.
        var fromLists = await Context.ExamQuestions
            .Where(row => row.QuestionId == questionId)
            .Select(row => row.ExamId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return fromPapers.Union(fromLists).ToList();
    }

    public async Task<IReadOnlyList<ExamQuestion>> GetQuestionRowsAsync(
        int examId,
        CancellationToken cancellationToken = default) =>
        await Context.ExamQuestions
            .Where(row => row.ExamId == examId)
            .OrderBy(row => row.Ordinal)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExamQuestion>> GetQuestionListAsync(
        int examId,
        CancellationToken cancellationToken = default) =>
        await Context.ExamQuestions
            .AsNoTracking()
            .Where(row => row.ExamId == examId)
            .Include(row => row.Question).ThenInclude(question => question.CreatedBy)
            .Include(row => row.Question).ThenInclude(question => question.Answers.OrderBy(answer => answer.Ordinal))
            .OrderBy(row => row.Ordinal)
            .ToListAsync(cancellationToken);

    public void RemoveQuestionRows(IEnumerable<ExamQuestion> rows) =>
        Context.ExamQuestions.RemoveRange(rows);

    public async Task AddQuestionRowsAsync(
        IEnumerable<ExamQuestion> rows,
        CancellationToken cancellationToken = default) =>
        await Context.ExamQuestions.AddRangeAsync(rows, cancellationToken);

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
