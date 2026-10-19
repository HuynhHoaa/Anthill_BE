using Anthill.Application.Common;
using Anthill.Application.DTOs.Terms;

namespace Anthill.Application.Interfaces.Services;

/// <summary>Quản lý danh mục kỳ học — chỉ quản trị viên. Kỳ là nhãn gom lớp, xem <c>AcademicTerm</c>.</summary>
public interface ITermService
{
    Task<Result<IReadOnlyList<AcademicTermDto>>> ListAsync(CancellationToken cancellationToken = default);

    Task<Result<AcademicTermDto>> CreateAsync(SaveAcademicTermRequest request, CancellationToken cancellationToken = default);

    Task<Result<AcademicTermDto>> UpdateAsync(int id, SaveAcademicTermRequest request, CancellationToken cancellationToken = default);

    /// <summary>Chỉ xoá được kỳ không còn lớp nào (409 nếu còn).</summary>
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
