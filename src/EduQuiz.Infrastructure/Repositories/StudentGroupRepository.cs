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

    public async Task<StudentGroup?> GetWithStudentsAsync(int groupId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(g => g.Students.Where(s => s.Role == UserRole.Student).OrderBy(s => s.Code))
            .Include(g => g.OwnerTeacher)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

    public async Task<IReadOnlyList<StudentGroup>> GetOwnedAsync(
        int? ownerTeacherId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();

        // Quản trị viên đọc toàn bộ nên truyền NULL; giáo viên chỉ thấy nhóm của mình.
        if (ownerTeacherId.HasValue)
        {
            query = query.Where(g => g.OwnerTeacherId == ownerTeacherId.Value);
        }

        if (SearchText.ToLikePattern(search) is { } pattern)
        {
            query = query.Where(g =>
                EF.Functions.ILike(EF.Functions.Unaccent(g.Name), pattern)
                || EF.Functions.ILike(EF.Functions.Unaccent(g.Description ?? string.Empty), pattern));
        }

        return await query
            .OrderBy(g => g.Name)
            .ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsOwnedByAsync(int groupId, int teacherId, CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(g => g.Id == groupId && g.OwnerTeacherId == teacherId, cancellationToken);

    public async Task<bool> IsNameTakenAsync(
        string name,
        int ownerTeacherId,
        int? excludeGroupId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var key = name.Trim().ToLowerInvariant();

        return await DbSet.AnyAsync(
            g => g.OwnerTeacherId == ownerTeacherId
                 && g.Name.ToLower() == key
                 && (excludeGroupId == null || g.Id != excludeGroupId),
            cancellationToken);
    }

    /// <summary>
    /// Lọc ra đúng những nhóm thuộc giáo viên đang gọi. Giao bài theo nhóm đi qua đây, nên
    /// giáo viên A đoán id nhóm của giáo viên B cũng chỉ nhận về danh sách rỗng (§5).
    /// </summary>
    public async Task<IReadOnlyList<StudentGroup>> GetOwnedByIdsAsync(
        IEnumerable<int> groupIds,
        int ownerTeacherId,
        CancellationToken cancellationToken = default)
    {
        var ids = groupIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await DbSet
            .AsNoTracking()
            .Where(g => ids.Contains(g.Id) && g.OwnerTeacherId == ownerTeacherId)
            .ToListAsync(cancellationToken);
    }
}
