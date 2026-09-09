using EduQuiz.Application.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore.Storage;

namespace EduQuiz.Infrastructure.UnitOfWork;

/// <summary>
/// Bọc <see cref="IDbContextTransaction"/> của EF Core lại sau một interface của tầng Application,
/// để service viết được transaction mà không cần biết EF Core tồn tại.
/// </summary>
/// <remarks>
/// Rời khối <c>await using</c> mà chưa commit đồng nghĩa rollback — EF Core tự làm việc đó trong
/// <c>DisposeAsync</c>. Đây chính là hành vi mong muốn khi có exception ném ra giữa chừng, và để
/// EF lo giúp thì lỗi gốc không bị một lần rollback hỏng ở đây che mất.
/// </remarks>
internal sealed class EfTransactionScope : ITransactionScope
{
    private readonly IDbContextTransaction _transaction;

    public EfTransactionScope(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        _transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        _transaction.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => _transaction.DisposeAsync();
}
