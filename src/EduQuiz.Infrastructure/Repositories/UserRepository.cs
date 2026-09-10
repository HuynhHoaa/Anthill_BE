using EduQuiz.Application.Common;
using EduQuiz.Application.Interfaces.Repositories;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;
using EduQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduQuiz.Infrastructure.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Vai trò cố ý <b>không</b> nằm trong điều kiện tra: §3 quy tắc ⑤ cấm tin trường <c>role</c>
    /// client gửi lên. Ai gõ đúng danh tính nào thì ra tài khoản của danh tính đó, vai trò đọc từ
    /// cột <c>Role</c> trong database.
    /// </summary>
    public async Task<User?> GetByLoginIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var key = Normalize(identifier);
        if (key is null)
        {
            return null;
        }

        return await DbSet
            .Include(u => u.Subject)
            .Include(u => u.Group)
            .FirstOrDefaultAsync(
                u => u.Email.ToLower() == key
                     || u.Username.ToLower() == key
                     || (u.Code != null && u.Code.ToLower() == key),
                cancellationToken);
    }

    public async Task<User?> GetProfileAsync(int userId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(u => u.Subject)
            .Include(u => u.Group)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var key = Normalize(email);
        return key is null
            ? null
            : await DbSet.FirstOrDefaultAsync(u => u.Email.ToLower() == key, cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var key = Normalize(username);
        return key is null
            ? null
            : await DbSet.FirstOrDefaultAsync(u => u.Username.ToLower() == key, cancellationToken);
    }

    public async Task<User?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var key = Normalize(code);
        return key is null
            ? null
            : await DbSet.FirstOrDefaultAsync(u => u.Code != null && u.Code.ToLower() == key, cancellationToken);
    }

    public async Task<bool> IsEmailTakenAsync(
        string email,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        var key = Normalize(email);
        return key is not null && await DbSet.AnyAsync(
            u => u.Email.ToLower() == key && (excludeUserId == null || u.Id != excludeUserId),
            cancellationToken);
    }

    public async Task<bool> IsUsernameTakenAsync(
        string username,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        var key = Normalize(username);
        return key is not null && await DbSet.AnyAsync(
            u => u.Username.ToLower() == key && (excludeUserId == null || u.Id != excludeUserId),
            cancellationToken);
    }

    public async Task<bool> IsCodeTakenAsync(
        string code,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        var key = Normalize(code);
        return key is not null && await DbSet.AnyAsync(
            u => u.Code != null && u.Code.ToLower() == key && (excludeUserId == null || u.Id != excludeUserId),
            cancellationToken);
    }

    public async Task<PagedResult<User>> GetStudentsPagedAsync(
        string? search,
        int? groupId,
        UserStatus? status,
        PaginationParams pagination,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Include(u => u.Group)
            .Where(u => u.Role == UserRole.Student);

        if (groupId.HasValue)
        {
            // 0 là UNASSIGNED_GROUP_ID của frontend, nghĩa là "chưa phân nhóm" — không phải một
            // nhóm có id bằng 0. Không tách nhánh này thì ô lọc "Chưa phân nhóm" luôn trả về rỗng,
            // và đó là loại lỗi trông hệt như "lớp này thật sự không có ai".
            query = groupId.Value == StudentGroupDefaults.UnassignedId
                ? query.Where(u => u.GroupId == null)
                : query.Where(u => u.GroupId == groupId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(u => u.Status == status.Value);
        }

        if (SearchText.ToLikePattern(search) is { } pattern)
        {
            query = query.Where(u =>
                EF.Functions.ILike(EF.Functions.Unaccent(u.FullName), pattern)
                || EF.Functions.ILike(u.Email, pattern)
                || EF.Functions.ILike(u.Username, pattern)
                || EF.Functions.ILike(u.Code ?? string.Empty, pattern));
        }

        // Mã học sinh là thứ tự tự nhiên của danh sách lớp; ThenBy(Id) để phân trang ổn định.
        query = query.OrderBy(u => u.Code).ThenBy(u => u.Id);

        return await query.ToPagedResultAsync(pagination, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetStudentsByGroupAsync(
        int groupId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Student && u.GroupId == groupId)
            .OrderBy(u => u.Code)
            .ThenBy(u => u.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Chỉ trả về id thật sự tồn tại và đúng vai trò Student. Giao bài dựa vào đây để một id lạ
    /// hay id của giáo viên lọt vào danh sách chọn cũng không tạo ra assignment (§5).
    /// </summary>
    public async Task<IReadOnlyList<User>> GetStudentsByIdsAsync(
        IEnumerable<int> studentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = studentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await DbSet
            .Where(u => u.Role == UserRole.Student && ids.Contains(u.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAssignableStudentsAsync(
        int groupId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Include(u => u.Group)
            .Where(u => u.Role == UserRole.Student && (u.GroupId == null || u.GroupId != groupId))
            .OrderBy(u => u.Code)
            .ThenBy(u => u.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<User>> GetAssignableTeachersAsync(
        int groupId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Include(u => u.Subject)
            .Where(u => u.Role == UserRole.Teacher
                        && !u.TeachingGroups.Any(t => t.StudentGroupId == groupId))
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<User>> GetTeachersByIdsAsync(
        IEnumerable<int> teacherIds,
        CancellationToken cancellationToken = default)
    {
        var ids = teacherIds?.Distinct().ToList() ?? [];
        if (ids.Count == 0)
        {
            return [];
        }

        // Lọc theo vai trò ngay trong truy vấn: chênh lệch giữa số id gửi lên và số dòng trả về
        // chính là số id không tồn tại hoặc không phải giáo viên — service đếm bằng phép trừ đó.
        return await DbSet
            .Include(u => u.Subject)
            .Where(u => ids.Contains(u.Id) && u.Role == UserRole.Teacher)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountStudentsInGroupAsync(int groupId, CancellationToken cancellationToken = default) =>
        await DbSet.CountAsync(u => u.Role == UserRole.Student && u.GroupId == groupId, cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> CountStudentsByGroupAsync(
        IEnumerable<int> groupIds,
        CancellationToken cancellationToken = default)
    {
        var ids = groupIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var counts = await DbSet
            .Where(u => u.Role == UserRole.Student && u.GroupId != null && ids.Contains(u.GroupId!.Value))
            .GroupBy(u => u.GroupId!.Value)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Nhóm rỗng phải xuất hiện với số 0, nếu không màn hình danh sách sẽ hiện ô trống.
        var result = ids.ToDictionary(id => id, _ => 0);
        foreach (var row in counts)
        {
            result[row.GroupId] = row.Count;
        }

        return result;
    }

    public async Task<int> CountByRoleAsync(UserRole role, CancellationToken cancellationToken = default) =>
        await DbSet.CountAsync(u => u.Role == role, cancellationToken);

    /// <summary>Trim rồi hạ chữ thường — §5 cấm để <c>'HS1001 '</c> và <c>'HS1001'</c> cùng lọt qua.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
