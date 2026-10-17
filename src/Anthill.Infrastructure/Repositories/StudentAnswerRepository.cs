using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Entities;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

public class StudentAnswerRepository : GenericRepository<StudentAnswer>, IStudentAnswerRepository
{
    public StudentAnswerRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<StudentAnswer>> GetByAttemptAsync(
        int attemptId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(s => s.AttemptId == attemptId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Chỉ những câu client vừa gửi. Lưu nháp mang ngữ nghĩa MERGE (§4.7): câu không có trong
    /// gói tin phải giữ nguyên lựa chọn cũ, nên không được nạp cả bộ rồi ghi đè.
    /// </summary>
    public async Task<IReadOnlyList<StudentAnswer>> GetByAttemptAndQuestionsAsync(
        int attemptId,
        IEnumerable<int> examPaperQuestionIds,
        CancellationToken cancellationToken = default)
    {
        var ids = examPaperQuestionIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await DbSet
            .Where(s => s.AttemptId == attemptId && ids.Contains(s.ExamPaperQuestionId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetSelectionMapAsync(
        int attemptId,
        CancellationToken cancellationToken = default)
    {
        var rows = await DbSet
            .AsNoTracking()
            .Where(s => s.AttemptId == attemptId)
            .Select(s => new { s.ExamPaperQuestionId, s.SelectedAnswerIds })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.ExamPaperQuestionId,
            row => (IReadOnlyList<int>)row.SelectedAnswerIds);
    }

    public async Task RemoveByAttemptAsync(int attemptId, CancellationToken cancellationToken = default)
    {
        var answers = await DbSet
            .Where(s => s.AttemptId == attemptId)
            .ToListAsync(cancellationToken);

        DbSet.RemoveRange(answers);
    }
}
