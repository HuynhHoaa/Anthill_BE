using EduQuiz.Application.Common;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Interfaces.Repositories;

/// <summary>
/// Một bảng User dùng chung cho ba vai trò. Mọi so trùng email/username/mã học sinh
/// đều phải trim và bỏ phân biệt hoa thường trước khi so (§5).
/// </summary>
public interface IUserRepository : IGenericRepository<User>
{
    /// <summary>
    /// Tra tài khoản theo chuỗi người dùng gõ ở ô đăng nhập: email (giáo viên, quản trị),
    /// username hoặc mã học sinh (học sinh). Vai trò <b>không</b> tham gia điều kiện tra —
    /// §3 quy tắc ⑤ cấm tin trường <c>role</c> client gửi lên.
    /// </summary>
    Task<User?> GetByLoginIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>Nạp kèm Subject và Group để dựng hồ sơ mà không cần truy vấn thêm.</summary>
    Task<User?> GetProfileAsync(int userId, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<User?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary><paramref name="excludeUserId"/> để khi sửa hồ sơ không tự đụng chính mình.</summary>
    Task<bool> IsEmailTakenAsync(string email, int? excludeUserId = null, CancellationToken cancellationToken = default);

    Task<bool> IsUsernameTakenAsync(string username, int? excludeUserId = null, CancellationToken cancellationToken = default);

    Task<bool> IsCodeTakenAsync(string code, int? excludeUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Danh sách học sinh có phân trang. Tìm kiếm bỏ dấu, không phân biệt hoa thường (§5).</summary>
    Task<PagedResult<User>> GetStudentsPagedAsync(
        string? search,
        int? groupId,
        UserStatus? status,
        PaginationParams pagination,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetStudentsByGroupAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>Chỉ trả về id thật sự tồn tại và đúng vai trò Student — dùng khi giao bài (§5).</summary>
    Task<IReadOnlyList<User>> GetStudentsByIdsAsync(IEnumerable<int> studentIds, CancellationToken cancellationToken = default);

    /// <summary>Học sinh chưa thuộc nhóm này, cho màn hình thêm thành viên.</summary>
    Task<IReadOnlyList<User>> GetAssignableStudentsAsync(int groupId, CancellationToken cancellationToken = default);

    Task<int> CountStudentsInGroupAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>Sĩ số của nhiều nhóm trong một truy vấn, tránh N+1 ở màn hình danh sách nhóm.</summary>
    Task<IReadOnlyDictionary<int, int>> CountStudentsByGroupAsync(
        IEnumerable<int> groupIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Giáo viên có thể phân công vào lớp — trừ những người đã được phân công vào chính lớp đó.
    /// </summary>
    /// <remarks>
    /// Bao gồm cả tài khoản đang bị khoá, và đó là chủ đích: quản trị viên cần thấy người đó trong
    /// danh sách để biết vì sao không phân công được, thay vì đi tìm một cái tên đã biến mất. Việc
    /// từ chối nằm ở tầng service, kèm lý do.
    /// </remarks>
    Task<IReadOnlyList<User>> GetAssignableTeachersAsync(
        int groupId,
        CancellationToken cancellationToken = default);

    /// <summary>Chỉ trả về id thật sự tồn tại và đúng vai trò Teacher — dùng khi phân công theo lô.</summary>
    Task<IReadOnlyList<User>> GetTeachersByIdsAsync(
        IEnumerable<int> teacherIds,
        CancellationToken cancellationToken = default);

    Task<int> CountByRoleAsync(UserRole role, CancellationToken cancellationToken = default);
}
