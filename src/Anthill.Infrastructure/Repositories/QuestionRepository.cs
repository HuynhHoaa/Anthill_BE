using Anthill.Application.Common;
using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

public class QuestionRepository : GenericRepository<Question>, IQuestionRepository
{
    public QuestionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<Question>> GetPagedAsync(
        int? subjectId,
        int currentUserId,
        string? search,
        string? topic,
        QuestionDifficulty? difficulty,
        QuestionStatus? status,
        PaginationParams pagination,
        CancellationToken cancellationToken = default)
    {
        var query = InScope(DbSet.AsNoTracking(), subjectId, currentUserId)
            .Include(q => q.CreatedBy)
            .Include(q => q.Answers.OrderBy(a => a.Ordinal));

        var filtered = ApplyFilters(query, search, topic, difficulty, status);

        return await filtered
            .OrderByDescending(q => q.CreatedAt)
            .ThenByDescending(q => q.Id)
            .ToPagedResultAsync(pagination, cancellationToken);
    }

    public async Task<Question?> GetWithAnswersAsync(int questionId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(q => q.CreatedBy)
            .Include(q => q.Subject)
            .Include(q => q.Answers.OrderBy(a => a.Ordinal))
            .FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken);

    public async Task<IReadOnlyList<string>> GetTopicsAsync(
        int? subjectId,
        int currentUserId,
        CancellationToken cancellationToken = default) =>
        await InScope(DbSet.AsNoTracking(), subjectId, currentUserId)
            .Select(q => q.Topic)
            .Distinct()
            .OrderBy(topic => topic)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Gộp ba lời gọi mà frontend đang bắn song song thành một truy vấn GROUP BY. Ba mức độ
    /// luôn có mặt trong kết quả, kể cả khi số câu là 0 — bước xem trước blueprint cần biết
    /// "thiếu bao nhiêu câu", nên một khoá vắng mặt sẽ thành lỗi hiển thị.
    /// </summary>
    public async Task<IReadOnlyDictionary<QuestionDifficulty, int>> CountByDifficultyAsync(
        int? subjectId,
        int currentUserId,
        string? topic = null,
        QuestionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = InScope(DbSet.AsNoTracking(), subjectId, currentUserId);
        query = ApplyFilters(query, search: null, topic, difficulty: null, status);

        var counts = await query
            .GroupBy(q => q.Difficulty)
            .Select(g => new { Difficulty = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var result = Enum.GetValues<QuestionDifficulty>().ToDictionary(value => value, _ => 0);
        foreach (var row in counts)
        {
            result[row.Difficulty] = row.Count;
        }

        return result;
    }

    /// <summary>
    /// Trả về <b>toàn bộ</b> ứng viên của một mức độ, sắp theo Id. Việc bốc ngẫu nhiên do tầng
    /// service làm bằng Fisher-Yates có seed: §5 đòi cùng một seed phải cho ra cùng một bộ câu
    /// sau khi học sinh F5, nên <c>ORDER BY RANDOM()</c> ở database là sai từ gốc.
    /// </summary>
    public async Task<IReadOnlyList<Question>> GetBlueprintCandidatesAsync(
        int subjectId,
        int currentUserId,
        string? topic,
        QuestionDifficulty difficulty,
        CancellationToken cancellationToken = default)
    {
        var query = InScope(DbSet.AsNoTracking(), subjectId, currentUserId)
            .Include(q => q.Answers.OrderBy(a => a.Ordinal));

        var filtered = ApplyFilters(query, search: null, topic, difficulty, QuestionStatus.Active);

        return await filtered
            .OrderBy(q => q.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsUsedInAnyPaperAsync(int questionId, CancellationToken cancellationToken = default) =>
        await Context.ExamPaperQuestions.AnyAsync(pq => pq.QuestionId == questionId, cancellationToken);

    public async Task<int> CountInSubjectAsync(int subjectId, CancellationToken cancellationToken = default) =>
        await DbSet.CountAsync(q => q.SubjectId == subjectId, cancellationToken);

    /// <summary>
    /// Phạm vi đọc của §6, áp cho <b>mọi</b> truy vấn đọc: ngân hàng dùng chung theo tổ bộ môn,
    /// cộng thêm các câu riêng tư do chính người gọi soạn. Quyền <i>ghi</i> kiểm ở tầng service.
    /// </summary>
    /// <remarks>
    /// <paramref name="subjectId"/> NULL nghĩa là quản trị viên: bỏ cả hai mệnh đề. Bỏ mệnh đề
    /// <c>Visibility</c> là có chủ ý — <c>Private</c> sinh ra để chống <b>lộ đề giữa hai lớp học
    /// cùng buổi</b> (§6), tức là giữa các đồng nghiệp cùng dạy. Quản trị viên không đứng lớp nên
    /// không nằm trong mối lo đó, và §6 vốn đã cho họ quyền <i>ghi</i> trên mọi câu; che phần đọc
    /// trong khi mở phần ghi là một hàng rào không bảo vệ được gì.
    /// </remarks>
    private static IQueryable<Question> InScope(IQueryable<Question> query, int? subjectId, int currentUserId) =>
        subjectId is null
            ? query
            : query.Where(q =>
                q.SubjectId == subjectId.Value
                && (q.Visibility == QuestionVisibility.Shared || q.CreatedByUserId == currentUserId));

    private static IQueryable<Question> ApplyFilters(
        IQueryable<Question> query,
        string? search,
        string? topic,
        QuestionDifficulty? difficulty,
        QuestionStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(topic))
        {
            var normalizedTopic = topic.Trim().ToLowerInvariant();
            query = query.Where(q => q.Topic.ToLower() == normalizedTopic);
        }

        if (difficulty.HasValue)
        {
            query = query.Where(q => q.Difficulty == difficulty.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(q => q.Status == status.Value);
        }

        if (SearchText.ToLikePattern(search) is { } pattern)
        {
            query = query.Where(q =>
                EF.Functions.ILike(EF.Functions.Unaccent(q.Content), pattern)
                || EF.Functions.ILike(EF.Functions.Unaccent(q.Topic), pattern));
        }

        return query;
    }
}
