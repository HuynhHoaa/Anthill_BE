using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Entities;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

public class AcademicTermRepository : GenericRepository<AcademicTerm>, IAcademicTermRepository
{
    public AcademicTermRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<AcademicTerm>> GetAllOrderedAsync(CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .OrderByDescending(t => t.IsCurrent)
            .ThenByDescending(t => t.StartsOn != null)
            .ThenByDescending(t => t.StartsOn)
            .ThenByDescending(t => t.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> CountGroupsAsync(CancellationToken cancellationToken = default)
    {
        var counts = await Context.Set<StudentGroup>()
            .AsNoTracking()
            .Where(g => g.AcademicTermId != null)
            .GroupBy(g => g.AcademicTermId!.Value)
            .Select(group => new { TermId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.TermId, row => row.Count, cancellationToken);

        var ids = await DbSet.AsNoTracking().Select(t => t.Id).ToListAsync(cancellationToken);

        return ids.ToDictionary(id => id, id => counts.GetValueOrDefault(id, 0));
    }

    public async Task<bool> IsNameTakenAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var key = name.Trim().ToLowerInvariant();

        return await DbSet.AnyAsync(t => t.Name.ToLower() == key && (excludeId == null || t.Id != excludeId), cancellationToken);
    }

    public async Task<IReadOnlyList<AcademicTerm>> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        await DbSet.Where(t => t.IsCurrent).ToListAsync(cancellationToken);
}
