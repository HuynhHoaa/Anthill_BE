using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Entities;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

public class SubjectRepository : GenericRepository<Subject>, ISubjectRepository
{
    public SubjectRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Subject?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(code);
        if (normalized is null)
        {
            return null;
        }

        return await DbSet.FirstOrDefaultAsync(s => s.Code.ToLower() == normalized, cancellationToken);
    }

    public async Task<bool> IsCodeTakenAsync(
        string code,
        int? excludeSubjectId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(code);
        if (normalized is null)
        {
            return false;
        }

        return await DbSet.AnyAsync(
            s => s.Code.ToLower() == normalized && (excludeSubjectId == null || s.Id != excludeSubjectId),
            cancellationToken);
    }

    /// <summary>Trim rồi hạ chữ thường trước khi so — §5 cấm để <c>'KHTN '</c> và <c>'khtn'</c> cùng lọt.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
