using Anthill.Application.Common;
using Anthill.Application.DTOs.Terms;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

/// <summary>Danh mục kỳ học — xem <see cref="ITermService"/>.</summary>
public sealed class TermService : ITermService
{
    private const string AdminOnlyMessage = "Chỉ quản trị viên mới quản lý được kỳ học.";
    private const string NotFoundMessage = "Không tìm thấy kỳ học này.";
    private const string NameTakenMessage = "Tên kỳ học đã tồn tại.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public TermService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<AcademicTermDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<IReadOnlyList<AcademicTermDto>>(AdminOnlyMessage);
        }

        var terms = await _unitOfWork.AcademicTerms.GetAllOrderedAsync(cancellationToken);
        var counts = await _unitOfWork.AcademicTerms.CountGroupsAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AcademicTermDto>>(terms.Select(term => ToDto(term, counts.GetValueOrDefault(term.Id, 0))).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<AcademicTermDto>> CreateAsync(SaveAcademicTermRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<AcademicTermDto>(AdminOnlyMessage);
        }

        var name = ImportText.Collapse(request.Name);
        if (await _unitOfWork.AcademicTerms.IsNameTakenAsync(name, null, cancellationToken))
        {
            return Result.Conflict<AcademicTermDto>(NameTakenMessage);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        if (request.IsCurrent)
        {
            await ClearCurrentAsync(cancellationToken);
        }

        var term = new AcademicTerm
        {
            Name = name,
            StartsOn = request.StartsOn,
            EndsOn = request.EndsOn,
            IsCurrent = request.IsCurrent
        };
        await _unitOfWork.AcademicTerms.AddAsync(term, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(ToDto(term, 0));
    }

    /// <inheritdoc />
    public async Task<Result<AcademicTermDto>> UpdateAsync(int id, SaveAcademicTermRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden<AcademicTermDto>(AdminOnlyMessage);
        }

        var term = await _unitOfWork.AcademicTerms.GetByIdAsync(id, cancellationToken);
        if (term is null)
        {
            return Result.NotFound<AcademicTermDto>(NotFoundMessage);
        }

        var name = ImportText.Collapse(request.Name);
        if (await _unitOfWork.AcademicTerms.IsNameTakenAsync(name, id, cancellationToken))
        {
            return Result.Conflict<AcademicTermDto>(NameTakenMessage);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        // Chỉ mục duy nhất của IsCurrent không cho hai kỳ cùng true dù trong một câu lệnh: tắt kỳ cũ và lưu TRƯỚC, rồi mới bật kỳ này.
        if (request.IsCurrent && !term.IsCurrent)
        {
            await ClearCurrentAsync(cancellationToken);
        }

        term.Name = name;
        term.StartsOn = request.StartsOn;
        term.EndsOn = request.EndsOn;
        term.IsCurrent = request.IsCurrent;
        term.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.AcademicTerms.Update(term);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var counts = await _unitOfWork.AcademicTerms.CountGroupsAsync(cancellationToken);

        return Result.Success(ToDto(term, counts.GetValueOrDefault(term.Id, 0)));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Forbidden(AdminOnlyMessage);
        }

        var term = await _unitOfWork.AcademicTerms.GetByIdAsync(id, cancellationToken);
        if (term is null)
        {
            return Result.NotFound(NotFoundMessage);
        }

        var counts = await _unitOfWork.AcademicTerms.CountGroupsAsync(cancellationToken);
        var groups = counts.GetValueOrDefault(id, 0);
        if (groups > 0)
        {
            return Result.Conflict($"Không thể xoá kỳ còn {groups} lớp. Hãy chuyển các lớp sang kỳ khác hoặc bỏ phân kỳ trước.");
        }

        _unitOfWork.AcademicTerms.Remove(term);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task ClearCurrentAsync(CancellationToken cancellationToken)
    {
        var current = await _unitOfWork.AcademicTerms.GetCurrentAsync(cancellationToken);
        if (current.Count == 0)
        {
            return;
        }

        foreach (var other in current)
        {
            other.IsCurrent = false;
            other.UpdatedAt = DateTime.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static AcademicTermDto ToDto(AcademicTerm term, int groupCount) => new()
    {
        Id = term.Id,
        Name = term.Name,
        StartsOn = term.StartsOn,
        EndsOn = term.EndsOn,
        IsCurrent = term.IsCurrent,
        GroupCount = groupCount
    };
}
