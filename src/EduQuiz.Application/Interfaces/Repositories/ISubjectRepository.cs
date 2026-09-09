using EduQuiz.Domain.Entities;

namespace EduQuiz.Application.Interfaces.Repositories;

/// <summary>Môn học — ranh giới của một ngân hàng câu hỏi dùng chung theo tổ bộ môn (§6).</summary>
public interface ISubjectRepository : IGenericRepository<Subject>
{
    Task<Subject?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<bool> IsCodeTakenAsync(string code, int? excludeSubjectId = null, CancellationToken cancellationToken = default);
}
