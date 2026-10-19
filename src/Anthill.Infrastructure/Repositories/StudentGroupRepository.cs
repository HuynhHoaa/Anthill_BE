using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

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

    public async Task<IReadOnlyList<StudentGroup>> GetByStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(g => g.Students.Any(s => s.Id == studentId))
            .OrderBy(g => g.Name)
            .ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> CountMembersAsync(
        IEnumerable<int> groupIds,
        CancellationToken cancellationToken = default)
    {
        var ids = groupIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var counts = await Context.Set<StudentGroupMember>()
            .AsNoTracking()
            .Where(member => ids.Contains(member.StudentGroupId))
            .GroupBy(member => member.StudentGroupId)
            .Select(group => new { GroupId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.GroupId, row => row.Count, cancellationToken);

        return ids.ToDictionary(id => id, id => counts.GetValueOrDefault(id, 0));
    }

    public async Task<IReadOnlySet<int>> GetMemberIdsAsync(
        int groupId,
        IEnumerable<int> studentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = studentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new HashSet<int>();
        }

        var found = await Context.Set<StudentGroupMember>()
            .AsNoTracking()
            .Where(m => m.StudentGroupId == groupId && ids.Contains(m.StudentId))
            .Select(m => m.StudentId)
            .ToListAsync(cancellationToken);

        return found.ToHashSet();
    }

    public async Task<StudentGroupMember?> GetMemberAsync(
        int groupId,
        int studentId,
        CancellationToken cancellationToken = default) =>
        await Context.Set<StudentGroupMember>()
            .FirstOrDefaultAsync(m => m.StudentGroupId == groupId && m.StudentId == studentId, cancellationToken);

    public async Task<IReadOnlyList<StudentGroupMember>> GetMembershipsOfStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default) =>
        await Context.Set<StudentGroupMember>()
            .Where(m => m.StudentId == studentId)
            .ToListAsync(cancellationToken);

    public void AddMember(StudentGroupMember member) => Context.Set<StudentGroupMember>().Add(member);

    public void RemoveMember(StudentGroupMember member) => Context.Set<StudentGroupMember>().Remove(member);

    public void RemoveMembers(IEnumerable<StudentGroupMember> members) =>
        Context.Set<StudentGroupMember>().RemoveRange(members);

    public async Task<StudentGroup?> GetWithTeachersAsync(
        int groupId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(g => g.AcademicTerm)
            .Include(g => g.Teachers)
                .ThenInclude(t => t.Teacher)
                    .ThenInclude(u => u.Subject)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

    public async Task<IReadOnlyList<StudentGroup>> GetAllAsync(
        string? search,
        bool includeDrafts,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Include(g => g.AcademicTerm)
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
            .Include(g => g.AcademicTerm)
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

    public async Task<IReadOnlyList<StudentGroup>> GetByTeacherAsync(
        int teacherId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(g => g.Teachers.Any(t => t.TeacherId == teacherId))
            .OrderBy(g => g.Name)
            .ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> CountByTeacherAsync(
        IEnumerable<int> teacherIds,
        CancellationToken cancellationToken = default)
    {
        var ids = teacherIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var counts = await Context.Set<StudentGroupTeacher>()
            .AsNoTracking()
            .Where(t => ids.Contains(t.TeacherId))
            .GroupBy(t => t.TeacherId)
            .Select(g => new { TeacherId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(row => row.TeacherId, row => row.Count);
    }

    public void AddTeacher(StudentGroupTeacher assignment) =>
        Context.Set<StudentGroupTeacher>().Add(assignment);

    public void RemoveTeacher(StudentGroupTeacher assignment) =>
        Context.Set<StudentGroupTeacher>().Remove(assignment);

    public async Task<bool> LockForUpdateAsync(int groupId, CancellationToken cancellationToken = default)
    {
        // Chỉ cần giữ khoá, không cần entity: đọc thẳng cột Id và không theo dõi dòng nào.
        var rows = await DbSet
            .FromSql($"""SELECT * FROM "StudentGroups" WHERE "Id" = {groupId} FOR UPDATE""")
            .AsNoTracking()
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        return rows.Count > 0;
    }

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
