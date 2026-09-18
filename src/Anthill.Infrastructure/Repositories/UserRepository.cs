using Anthill.Application.Common;
using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

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

        // OrderBy(Id) là hàng rào phòng thủ thứ hai: hàng rào thứ nhất là IsEmailTakenAsync /
        // IsUsernameTakenAsync / IsCodeTakenAsync coi ba cột là MỘT không gian tên khi tạo/sửa, nên
        // về lý thuyết không còn hai tài khoản nào trùng nhau giữa các cột nữa. Nhưng dữ liệu cũ tạo
        // trước bản sửa này (hoặc nạp thẳng bằng script, bỏ qua tầng service) vẫn có thể phạm — khi
        // đó FirstOrDefaultAsync không có ORDER BY sẽ ra bất kỳ dòng nào Postgres tình cờ trả về
        // trước, đăng nhập sai tài khoản một cách không thể đoán trước. Có ORDER BY thì ít nhất kết
        // quả cố định (luôn cùng một dòng, luôn cùng một tài khoản) thay vì trồi sụt giữa các lần gọi.
        return await DbSet
            .Include(u => u.Subject)
            .Include(u => u.Group)
            .Where(u => u.Email.ToLower() == key
                        || u.Username.ToLower() == key
                        || (u.Code != null && u.Code.ToLower() == key))
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);
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

    /// <summary>
    /// Email, username và mã học sinh dùng chung MỘT không gian tên: một giá trị đã xuất hiện ở bất
    /// kỳ cột nào trong ba cột đó, của bất kỳ tài khoản nào, thì không còn dùng được cho cột khác nữa.
    /// </summary>
    /// <remarks>
    /// Trước bản sửa này, ba hàm <c>IsEmailTakenAsync</c>/<c>IsUsernameTakenAsync</c>/<c>IsCodeTakenAsync</c>
    /// mỗi hàm chỉ soi đúng cột của mình — tạo học sinh với <c>username</c> trùng hệt <c>email</c> của
    /// một tài khoản khác thì qua được cả kiểm tra khả dụng lẫn bước tạo, vì không hàm nào nhìn sang
    /// cột bên cạnh. Hậu quả lộ ra ở <see cref="GetByLoginIdentifierAsync"/>: tra theo chuỗi đó ra hai
    /// dòng, và tài khoản bị khoá nhầm là tài khoản không hề gõ sai gì. Ba hàm dưới đây giờ đều gọi
    /// chung một điều kiện tra trên cả ba cột, khớp đúng cách <c>GetByLoginIdentifierAsync</c> đã tra.
    /// </remarks>
    public async Task<bool> IsEmailTakenAsync(
        string email,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default) =>
        await IsIdentifierTakenAsync(email, excludeUserId, cancellationToken);

    public async Task<bool> IsUsernameTakenAsync(
        string username,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default) =>
        await IsIdentifierTakenAsync(username, excludeUserId, cancellationToken);

    public async Task<bool> IsCodeTakenAsync(
        string code,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default) =>
        await IsIdentifierTakenAsync(code, excludeUserId, cancellationToken);

    private async Task<bool> IsIdentifierTakenAsync(
        string identifier,
        int? excludeUserId,
        CancellationToken cancellationToken)
    {
        var key = Normalize(identifier);
        return key is not null && await DbSet.AnyAsync(
            u => (u.Email.ToLower() == key
                  || u.Username.ToLower() == key
                  || (u.Code != null && u.Code.ToLower() == key))
                 && (excludeUserId == null || u.Id != excludeUserId),
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

        return await ApplyStudentFilters(query, search, groupId, status)
            .ToPagedResultAsync(pagination, cancellationToken);
    }

    public async Task<PagedResult<User>> GetStudentsPagedForTeacherAsync(
        int teacherId,
        string? search,
        int? groupId,
        UserStatus? status,
        PaginationParams pagination,
        CancellationToken cancellationToken = default)
    {
        // Phạm vi đặt TRƯỚC mọi bộ lọc người dùng gửi lên và nằm trong cùng mệnh đề WHERE, nên không
        // tổ hợp groupId / status / search nào nới được nó ra. groupId của lớp khác hay groupId = 0
        // chỉ còn giao với tập rỗng.
        var query = DbSet
            .AsNoTracking()
            .Include(u => u.Group)
            .Where(u => u.Role == UserRole.Student)
            .Where(TeacherStudentScope.InTaughtClass(teacherId));

        return await ApplyStudentFilters(query, search, groupId, status)
            .ToPagedResultAsync(pagination, cancellationToken);
    }

    public async Task<User?> GetStudentProfileForTeacherAsync(
        int studentId,
        int teacherId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(u => u.Group)
            .Where(u => u.Id == studentId && u.Role == UserRole.Student)
            .Where(TeacherStudentScope.CanReadProfile(teacherId))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Ba bộ lọc của màn hình danh sách và thứ tự sắp xếp, dùng chung cho quản trị viên và giáo viên.
    /// </summary>
    /// <remarks>
    /// Dùng chung phần lọc chứ không dùng chung phần phạm vi: hai hàm public ở trên mỗi hàm tự dựng
    /// điểm xuất phát của mình, nên không có tham số nào để truyền nhầm.
    /// </remarks>
    private static IQueryable<User> ApplyStudentFilters(
        IQueryable<User> query,
        string? search,
        int? groupId,
        UserStatus? status)
    {
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
        return query.OrderBy(u => u.Code).ThenBy(u => u.Id);
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
            // Không liệt kê tài khoản chờ xoá hay đã xoá: sau 60 ngày chúng hiện ra dưới tên
            // "Học sinh đã xoá" cùng mã deleted-N, và "Chọn tất cả" sẽ đưa chúng trở lại lớp.
            .Where(u => u.Role == UserRole.Student
                        && u.Status != UserStatus.PendingDeletion
                        && u.Status != UserStatus.Deleted
                        && (u.GroupId == null || u.GroupId != groupId))
            .OrderBy(u => u.Code)
            .ThenBy(u => u.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// <c>SELECT *</c> không trả cột hệ thống, nên <c>xmin</c> phải ghi tường minh: thiếu nó EF không
    /// dựng được entity, và concurrency token cũng không có giá trị gốc để so.
    /// </remarks>
    public async Task<User?> LockForUpdateAsync(int userId, CancellationToken cancellationToken = default)
    {
        // Phải hỏi trước khi truy vấn, vì ngay sau nó thì dòng nào cũng đã nằm trong change tracker.
        var trackedBefore = Context.ChangeTracker
            .Entries<User>()
            .Any(entry => entry.Entity.Id == userId);

        var rows = await DbSet
            .FromSql($"""SELECT *, xmin FROM "Users" WHERE "Id" = {userId} FOR UPDATE""")
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var user = rows[0];

        if (trackedBefore)
        {
            // Cùng lý do với ExamAttemptRepository.LockForUpdateAsync: entity đã theo dõi giữ giá trị
            // của lần đọc trước lúc xếp hàng chờ khoá. Đọc lại khi đã giữ khoá thì không ai chen được.
            await Context.Entry(user).ReloadAsync(cancellationToken);
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> LockDueForDeletionAsync(
        DateTime dueBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var pending = nameof(UserStatus.PendingDeletion);

        return await DbSet
            .FromSql($"""
                SELECT *, xmin FROM "Users"
                WHERE "Status" = {pending}
                  AND "DeletionRequestedAt" IS NOT NULL
                  AND "DeletionRequestedAt" <= {dueBeforeUtc}
                ORDER BY "DeletionRequestedAt", "Id"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);
    }

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
        await DbSet.CountAsync(
            u => u.Role == UserRole.Student
                 && u.GroupId == groupId
                 && u.Status != UserStatus.PendingDeletion
                 && u.Status != UserStatus.Deleted,
            cancellationToken);

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
            .Where(u => u.Role == UserRole.Student
                        && u.GroupId != null
                        && ids.Contains(u.GroupId!.Value)
                        && u.Status != UserStatus.PendingDeletion
                        && u.Status != UserStatus.Deleted)
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
