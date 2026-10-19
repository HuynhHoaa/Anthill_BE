using Anthill.Domain.Entities;

namespace Anthill.Application.Interfaces.Repositories;

/// <summary>
/// Lịch sử khoá, xoá, gỡ khỏi lớp và chuyển lớp của học sinh, và khoá / mở khoá giáo viên — xem <see cref="AccountChange"/>.
/// </summary>
public interface IAccountChangeRepository : IGenericRepository<AccountChange>
{
    /// <summary>Lịch sử của một tài khoản, mới nhất trước, kèm tên người thực hiện.</summary>
    Task<IReadOnlyList<AccountChange>> GetByAccountAsync(int accountId, CancellationToken cancellationToken = default);
}
