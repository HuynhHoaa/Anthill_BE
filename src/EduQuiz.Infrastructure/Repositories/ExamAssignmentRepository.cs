using EduQuiz.Application.Interfaces.Repositories;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;
using EduQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduQuiz.Infrastructure.Repositories;

public class ExamAssignmentRepository : GenericRepository<ExamAssignment>, IExamAssignmentRepository
{
    public ExamAssignmentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ExamAssignment>> GetByExamAsync(
        int examId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Include(a => a.Student)
                .ThenInclude(s => s.Group)
            .Include(a => a.Attempt)
            .Where(a => a.ExamId == examId)
            .OrderBy(a => a.Student.Code)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

    public async Task<ExamAssignment?> GetByExamAndStudentAsync(
        int examId,
        int studentId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(a => a.Attempt)
            .FirstOrDefaultAsync(a => a.ExamId == examId && a.StudentId == studentId, cancellationToken);

    /// <summary>Nạp kèm Exam, Student, Attempt và bản đề riêng — mọi kiểm tra quyền và hạn giờ đều cần.</summary>
    public async Task<ExamAssignment?> GetDetailAsync(
        int assignmentId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(a => a.Exam)
                .ThenInclude(e => e.CreatedBy)
            .Include(a => a.Student)
            .Include(a => a.Attempt)
            .Include(a => a.Paper)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);

    public async Task<IReadOnlyList<int>> GetAssignedStudentIdsAsync(
        int examId,
        IEnumerable<int> studentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = studentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await DbSet
            .AsNoTracking()
            .Where(a => a.ExamId == examId && ids.Contains(a.StudentId))
            .Select(a => a.StudentId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExamAssignment>> GetForStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Include(a => a.Exam)
                .ThenInclude(e => e.CreatedBy)
            .Include(a => a.Attempt)
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

    public async Task<bool> IsOwnedByStudentAsync(
        int assignmentId,
        int studentId,
        CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(a => a.Id == assignmentId && a.StudentId == studentId, cancellationToken);

    public async Task<int> CountByExamAsync(int examId, CancellationToken cancellationToken = default) =>
        await DbSet.CountAsync(a => a.ExamId == examId, cancellationToken);

    public async Task<int> CountSubmittedByExamAsync(int examId, CancellationToken cancellationToken = default) =>
        await Context.ExamAttempts.CountAsync(
            attempt => attempt.Assignment.ExamId == examId
                       && (attempt.Status == AssignmentStatus.Submitted || attempt.Status == AssignmentStatus.Graded),
            cancellationToken);

    /// <summary>
    /// Hai con số dẫn xuất cho nhiều đề trong hai truy vấn cố định, thay cho 2×N lời gọi ở màn hình
    /// danh sách đề. Cố ý tách làm hai lượt <c>GROUP BY</c> đơn giản thay vì một truy vấn có
    /// <c>COUNT</c> điều kiện lồng qua navigation — hai lượt này chắc chắn dịch được sang SQL.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, (int Assigned, int Submitted)>> CountByExamsAsync(
        IEnumerable<int> examIds,
        CancellationToken cancellationToken = default)
    {
        var ids = examIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, (int, int)>();
        }

        var assigned = await DbSet
            .AsNoTracking()
            .Where(a => ids.Contains(a.ExamId))
            .GroupBy(a => a.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var submitted = await Context.ExamAttempts
            .AsNoTracking()
            .Where(attempt => ids.Contains(attempt.Assignment.ExamId)
                              && (attempt.Status == AssignmentStatus.Submitted || attempt.Status == AssignmentStatus.Graded))
            .GroupBy(attempt => attempt.Assignment.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var submittedByExam = submitted.ToDictionary(row => row.ExamId, row => row.Count);
        var assignedByExam = assigned.ToDictionary(row => row.ExamId, row => row.Count);

        // Đề chưa giao cho ai vẫn phải có mặt với (0, 0) — thiếu khoá là màn hình hiện ô trống.
        return ids.ToDictionary(
            id => id,
            id => (
                Assigned: assignedByExam.GetValueOrDefault(id),
                Submitted: submittedByExam.GetValueOrDefault(id)));
    }

    public async Task RemoveByExamAsync(int examId, CancellationToken cancellationToken = default)
    {
        var assignments = await DbSet
            .Where(a => a.ExamId == examId)
            .ToListAsync(cancellationToken);

        // Attempt, StudentAnswer và bản đề riêng đi theo nhờ ON DELETE CASCADE ở database.
        DbSet.RemoveRange(assignments);
    }
}
