using Anthill.Application.Common;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Interfaces.Repositories;

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

    /// <summary>
    /// Danh sách học sinh <b>trong phạm vi một giáo viên</b>: chỉ các em ở lớp giáo viên đó được phân
    /// công và lớp đã công bố (<see cref="TeacherStudentScope.InTaughtClass"/>).
    /// </summary>
    /// <remarks>
    /// Tách hẳn khỏi <see cref="GetStudentsPagedAsync"/> thay vì thêm một tham số "phạm vi" có thể
    /// NULL: một ngày nào đó truyền nhầm NULL là giáo viên đọc được tài khoản của cả trường. Bộ lọc
    /// <paramref name="groupId"/>, <paramref name="status"/>, <paramref name="search"/> chạy <b>bên
    /// trong</b> phạm vi, nên lọc theo lớp của người khác chỉ ra trang rỗng — không lỗi, không rò.
    /// </remarks>
    Task<PagedResult<User>> GetStudentsPagedForTeacherAsync(
        int teacherId,
        string? search,
        int? groupId,
        UserStatus? status,
        PaginationParams pagination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hồ sơ một học sinh (nạp kèm Group) nếu giáo viên được phép xem, ngược lại NULL
    /// (<see cref="TeacherStudentScope.CanReadProfile"/>).
    /// </summary>
    /// <remarks>
    /// Không phân biệt "không có" với "không được xem" — chỗ gọi biến cả hai thành 404, để giáo viên
    /// không dò ra được id nào là học sinh có thật.
    /// </remarks>
    Task<User?> GetStudentProfileForTeacherAsync(
        int studentId,
        int teacherId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetStudentsByGroupAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>Chỉ trả về id thật sự tồn tại và đúng vai trò Student — dùng khi giao bài (§5).</summary>
    Task<IReadOnlyList<User>> GetStudentsByIdsAsync(IEnumerable<int> studentIds, CancellationToken cancellationToken = default);

    /// <summary>Học sinh chưa thuộc nhóm này, cho màn hình thêm thành viên.</summary>
    Task<IReadOnlyList<User>> GetAssignableStudentsAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sĩ số của lớp: học sinh đang hoạt động hoặc đang khoá. Tài khoản chờ xoá không được tính.
    /// </summary>
    /// <remarks>
    /// Tài khoản chờ xoá vẫn giữ GroupId suốt 60 ngày để khôi phục là về đúng lớp cũ, nên vẫn hiện
    /// trong danh sách thành viên. Nhưng em đó đã rời trường: đếm vào sĩ số thì câu "giao bài cho
    /// 30 học sinh" sai ngay từ đầu, vì bước giao bài bỏ qua mọi tài khoản không hoạt động.
    /// </remarks>
    Task<int> CountStudentsInGroupAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sĩ số của nhiều nhóm trong một truy vấn, tránh N+1 ở màn hình danh sách nhóm. Cùng quy tắc
    /// với <see cref="CountStudentsInGroupAsync"/>.
    /// </summary>
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

    /// <summary>
    /// Nạp một tài khoản kèm <b>khoá ghi ở mức dòng</b>, giữ tới khi transaction kết thúc.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chỉ có nghĩa khi gọi bên trong một transaction. Dùng ở mọi lối ghi quyết định phiên đăng nhập:
    /// khoá, cấp lại mật khẩu, đổi mật khẩu, đưa vào diện xoá, khôi phục.
    /// </para>
    /// <para>
    /// Concurrency token (xmin) một mình thì chỉ nói "có người ghi chen vào" và để bên đến sau nhận
    /// 409 — mà bên đến sau có thể chính là quản trị viên đang bấm khoá, trong khi một tài khoản bị
    /// chiếm bắn liên tục <c>PUT /api/auth/me</c> để lệnh khoá lần nào cũng trượt. Giữ khoá dòng từ
    /// lúc đọc tới lúc commit thì không ai ghi chen được: thao tác bảo mật luôn xong, và request cũ
    /// ghi sau nó mới là bên nhận 409.
    /// </para>
    /// </remarks>
    Task<User?> LockForUpdateAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Tài khoản đã hết thời gian chờ, đến lúc xoá dữ liệu cá nhân — khoá dòng, bỏ qua dòng đang bị khoá.</summary>
    /// <remarks>
    /// <para>
    /// Lọc ngay trong SQL theo mốc thời gian truyền vào chứ không đọc hết rồi lọc trong bộ nhớ:
    /// bảng người dùng lớn dần theo từng khoá học sinh, còn số tài khoản đến hạn thì luôn nhỏ.
    /// </para>
    /// <para>
    /// Chỉ có nghĩa bên trong một transaction. Dòng đang bị khoá — quản trị viên đang khôi phục đúng
    /// tài khoản đó — bị bỏ qua (<c>SKIP LOCKED</c>) thay vì chờ: ẩn danh hoá không hoàn tác được, nên
    /// thà để lượt quét sau xét lại còn hơn ghi lên một tài khoản vừa được khôi phục.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<User>> LockDueForDeletionAsync(
        DateTime dueBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<int> CountByRoleAsync(UserRole role, CancellationToken cancellationToken = default);
}
