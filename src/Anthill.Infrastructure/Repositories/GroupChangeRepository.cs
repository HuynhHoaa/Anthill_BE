using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Entities;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

public class GroupChangeRepository : GenericRepository<GroupChange>, IGroupChangeRepository
{
    public GroupChangeRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<GroupChange>> GetByGroupAsync(
        int groupId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Include(c => c.Actor)
            .Where(c => c.GroupId == groupId)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);
}
