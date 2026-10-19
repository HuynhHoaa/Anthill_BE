using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Entities;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

public class AccountChangeRepository : GenericRepository<AccountChange>, IAccountChangeRepository
{
    public AccountChangeRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<AccountChange>> GetByAccountAsync(
        int accountId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Include(c => c.Actor)
            .Where(c => c.AccountId == accountId)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);
}
