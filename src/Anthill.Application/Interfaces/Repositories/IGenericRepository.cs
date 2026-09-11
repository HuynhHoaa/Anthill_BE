using System.Linq.Expressions;
using Anthill.Domain.Common;

namespace Anthill.Application.Interfaces.Repositories;

/// <summary>
/// Thao tác chung cho mọi entity. Các lệnh ghi (<see cref="Update"/>, <see cref="Remove"/>)
/// chỉ đánh dấu thay đổi; việc ghi xuống database do
/// <c>IUnitOfWork.SaveChangesAsync</c> thực hiện một lần cho cả nghiệp vụ.
/// </summary>
public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>Đếm toàn bảng khi <paramref name="predicate"/> là NULL.</summary>
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    void Update(T entity);

    void Remove(T entity);

    void RemoveRange(IEnumerable<T> entities);
}
