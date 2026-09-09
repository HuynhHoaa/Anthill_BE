using EduQuiz.Domain.Entities;

namespace EduQuiz.Application.Interfaces.Repositories;

/// <summary>
/// Nhóm lớp thuộc về đúng một giáo viên. <c>OwnerTeacherId</c> là thứ chặn giáo viên A
/// giao bài cho lớp của giáo viên B bằng cách đoán id (§5).
/// </summary>
public interface IStudentGroupRepository : IGenericRepository<StudentGroup>
{
    Task<StudentGroup?> GetWithStudentsAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>Nhóm do giáo viên này quản lý; quản trị viên đọc toàn bộ nên truyền NULL.</summary>
    Task<IReadOnlyList<StudentGroup>> GetOwnedAsync(
        int? ownerTeacherId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<bool> IsOwnedByAsync(int groupId, int teacherId, CancellationToken cancellationToken = default);

    /// <summary>Trùng tên xét trong phạm vi một giáo viên, so sánh sau khi trim và bỏ hoa thường.</summary>
    Task<bool> IsNameTakenAsync(
        string name,
        int ownerTeacherId,
        int? excludeGroupId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lọc ra các nhóm thuộc giáo viên này — dùng khi giao bài theo nhiều nhóm cùng lúc.</summary>
    Task<IReadOnlyList<StudentGroup>> GetOwnedByIdsAsync(
        IEnumerable<int> groupIds,
        int ownerTeacherId,
        CancellationToken cancellationToken = default);
}
