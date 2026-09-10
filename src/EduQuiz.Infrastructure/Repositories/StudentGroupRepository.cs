using EduQuiz.Application.Interfaces.Repositories;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;
using EduQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduQuiz.Infrastructure.Repositories;

public class StudentGroupRepository : GenericRepository<StudentGroup>, IStudentGroupRepository
{
    public StudentGroupRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<StudentGroup?> GetWithStudentsAsync(
        int groupId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(g => g.Students.Where(s => s.Role == UserRole.Student).OrderBy(s => s.Code))
            .Include(g => g.CreatedBy)
            .Include(g => g.Teachers)
                .ThenInclude(t => t.Teacher)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

    public async Task<StudentGroup?> GetWithTeachersAsync(
        int groupId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(g => g.Teachers)
                .ThenInclude(t => t.Teacher)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

    public async Task<IReadOnlyList<StudentGroup>> GetAllAsync(
        string? search,
        bool includeDrafts,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Include(g => g.Teachers)
                .ThenInclude(t => t.Teacher)
            .AsQueryable();

        if (!includeDrafts)
        {
            query = query.Where(g => g.IsPublished);
        }

        return await OrderForDisplay(ApplySearch(query, search)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudentGroup>> GetForTeacherAsync(
        int teacherId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        // Hai điều kiện đi liền nhau ở mọi truy vấn phục vụ giáo viên: được phân công, VÀ đã công
        // bố. Tách rời chúng ra là sớm muộn có chỗ chỉ kiểm một vế và làm lộ lớp đang dựng dở.
        var query = DbSet
            .AsNoTracking()
            .Include(g => g.Teachers)
                .ThenInclude(t => t.Teacher)
            .Where(g => g.IsPublished && g.Teachers.Any(t => t.TeacherId == teacherId));

        return await OrderForDisplay(ApplySearch(query, search)).ToListAsync(cancellationToken);
    }

    public async Task<bool> IsTeachingAsync(
        int groupId,
        int teacherId,
        CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(
            g => g.Id == groupId && g.IsPublished && g.Teachers.Any(t => t.TeacherId == teacherId),
            cancellationToken);

    public async Task<bool> IsNameTakenAsync(
        string name,
        int? excludeGroupId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var key = name.Trim().ToLowerInvariant();

        return await DbSet.AnyAsync(
            g => g.Name.ToLower() == key && (excludeGroupId == null || g.Id != excludeGroupId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<StudentGroup>> GetTeachableByIdsAsync(
        IEnumerable<int> groupIds,
        int teacherId,
        CancellationToken cancellationToken = default)
    {
        var ids = groupIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await DbSet
            .AsNoTracking()
            .Where(g => ids.Contains(g.Id)
                        && g.IsPublished
                        && g.Teachers.Any(t => t.TeacherId == teacherId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAssignedTeachersAsync(
        int groupId,
        CancellationToken cancellationToken = default) =>
        await Context.Set<StudentGroupTeacher>()
            .AsNoTracking()
            .Where(t => t.StudentGroupId == groupId)
            .Select(t => t.Teacher)
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);

    public void AddTeacher(StudentGroupTeacher assignment) =>
        Context.Set<StudentGroupTeacher>().Add(assignment);

    public void RemoveTeacher(StudentGroupTeacher assignment) =>
        Context.Set<StudentGroupTeacher>().Remove(assignment);

    public async Task<StudentGroupTeacher?> FindAssignmentAsync(
        int groupId,
        int teacherId,
        CancellationToken cancellationToken = default) =>
        await Context.Set<StudentGroupTeacher>()
            .FirstOrDefaultAsync(
                t => t.StudentGroupId == groupId && t.TeacherId == teacherId,
                cancellationToken);

    private static IQueryable<StudentGroup> ApplySearch(IQueryable<StudentGroup> query, string? search)
    {
        if (SearchText.ToLikePattern(search) is not { } pattern)
        {
            return query;
        }

        return query.Where(g =>
            EF.Functions.ILike(EF.Functions.Unaccent(g.Name), pattern)
            || EF.Functions.ILike(EF.Functions.Unaccent(g.Description ?? string.Empty), pattern));
    }

    /// <summary>Bản nháp lên trước: đó là những lớp còn dở dang, cần người làm nốt.</summary>
    private static IQueryable<StudentGroup> OrderForDisplay(IQueryable<StudentGroup> query) =>
        query
            .OrderBy(g => g.IsPublished)
            .ThenBy(g => g.Name)
            .ThenBy(g => g.Id);
}
