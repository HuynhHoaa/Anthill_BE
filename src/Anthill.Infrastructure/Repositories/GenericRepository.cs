using System.Linq.Expressions;
using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Common;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

/// <summary>
/// Thao tác chung cho mọi entity.
/// </summary>
/// <remarks>
/// <see cref="Update"/>, <see cref="Remove"/> và <see cref="RemoveRange"/> chỉ đánh dấu thay đổi
/// trên change tracker; việc ghi xuống database do <c>IUnitOfWork.SaveChangesAsync</c> thực hiện
/// một lần cho cả nghiệp vụ. Vì vậy các repository con cũng <b>không</b> dùng
/// <c>ExecuteDelete</c>/<c>ExecuteUpdate</c> — chúng ghi ngay lập tức, phá vỡ ranh giới đó.
/// </remarks>
public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    public GenericRepository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    protected AppDbContext Context { get; }

    protected DbSet<T> DbSet { get; }

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await DbSet.FindAsync(new object?[] { id }, cancellationToken);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking().ToListAsync(cancellationToken);

    public virtual async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await DbSet.Where(predicate).ToListAsync(cancellationToken);

    public virtual async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await DbSet.FirstOrDefaultAsync(predicate, cancellationToken);

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(predicate, cancellationToken);

    public virtual async Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default) =>
        predicate is null
            ? await DbSet.CountAsync(cancellationToken)
            : await DbSet.CountAsync(predicate, cancellationToken);

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await DbSet.AddAsync(entity, cancellationToken);

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) =>
        await DbSet.AddRangeAsync(entities, cancellationToken);

    public virtual void Update(T entity) => DbSet.Update(entity);

    public virtual void Remove(T entity) => DbSet.Remove(entity);

    public virtual void RemoveRange(IEnumerable<T> entities) => DbSet.RemoveRange(entities);
}
