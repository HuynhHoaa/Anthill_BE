using Anthill.Domain.Entities;

namespace Anthill.Application.Interfaces.Repositories;

/// <summary>Kỳ học — nhãn gom lớp theo thời gian, xem <see cref="AcademicTerm"/>.</summary>
public interface IAcademicTermRepository : IGenericRepository<AcademicTerm>
{
    /// <summary>Mọi kỳ, kỳ hiện tại trước, rồi kỳ mới bắt đầu trước (kỳ không có ngày xếp sau), rồi theo id giảm dần.</summary>
    Task<IReadOnlyList<AcademicTerm>> GetAllOrderedAsync(CancellationToken cancellationToken = default);

    /// <summary>Số lớp của từng kỳ (kể cả lớp nháp); kỳ chưa có lớp vẫn có mặt với 0.</summary>
    Task<IReadOnlyDictionary<int, int>> CountGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>Tên kỳ đã có chưa (không phân biệt hoa thường), bỏ qua kỳ <paramref name="excludeId"/> khi đang sửa chính nó.</summary>
    Task<bool> IsNameTakenAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Các kỳ đang được đánh dấu hiện tại (bình thường tối đa một), nạp để tắt cờ trước khi đặt kỳ khác.</summary>
    Task<IReadOnlyList<AcademicTerm>> GetCurrentAsync(CancellationToken cancellationToken = default);
}
