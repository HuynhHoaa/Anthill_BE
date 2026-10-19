using Anthill.Domain.Entities;

namespace Anthill.Application.Interfaces.Repositories;

/// <summary>Lịch sử của lớp: lập lớp và đổi giáo viên phụ trách — xem <see cref="GroupChange"/>.</summary>
public interface IGroupChangeRepository : IGenericRepository<GroupChange>
{
    /// <summary>Lịch sử của một lớp, mới nhất trước, kèm tên người thực hiện.</summary>
    Task<IReadOnlyList<GroupChange>> GetByGroupAsync(int groupId, CancellationToken cancellationToken = default);
}
