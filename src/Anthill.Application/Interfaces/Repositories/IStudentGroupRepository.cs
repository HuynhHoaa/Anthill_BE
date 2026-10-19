using Anthill.Domain.Entities;

namespace Anthill.Application.Interfaces.Repositories;

/// <summary>
/// Lớp học — đơn vị hành chính của trung tâm.
/// </summary>
/// <remarks>
/// <para>
/// Quản trị viên lập lớp; giáo viên được <b>phân công</b> vào lớp qua bảng
/// <c>StudentGroupTeachers</c>. Phạm vi nhìn thấy của giáo viên đến từ bảng phân công đó, không
/// còn từ việc "ai tạo ra lớp" như mô hình cũ.
/// </para>
/// <para>
/// Lớp còn ở dạng nháp chỉ quản trị viên thấy. Mọi truy vấn phục vụ giáo viên hoặc học sinh ở đây
/// đều đã lọc <c>IsPublished</c> sẵn — để một chỗ quên lọc không thể làm lộ lớp chưa công bố.
/// </para>
/// </remarks>
public interface IStudentGroupRepository : IGenericRepository<StudentGroup>
{
    /// <summary>Nạp kèm danh sách học sinh và các giáo viên được phân công.</summary>
    Task<StudentGroup?> GetWithStudentsAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>Nạp kèm bảng phân công giáo viên, cho màn hình quản trị.</summary>
    Task<StudentGroup?> GetWithTeachersAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Danh sách lớp cho quản trị viên — thấy tất cả, kể cả bản nháp.
    /// </summary>
    Task<IReadOnlyList<StudentGroup>> GetAllAsync(
        string? search,
        bool includeDrafts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Danh sách lớp của một giáo viên: <b>chỉ lớp được phân công và đã công bố</b>.
    /// </summary>
    Task<IReadOnlyList<StudentGroup>> GetForTeacherAsync(
        int teacherId,
        string? search,
        CancellationToken cancellationToken = default);

    /// <summary>Giáo viên này có được phân công vào lớp đó không, và lớp đã công bố chưa.</summary>
    Task<bool> IsTeachingAsync(int groupId, int teacherId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tên lớp đã có ai dùng chưa — xét trên <b>toàn hệ thống</b>.
    /// </summary>
    /// <remarks>
    /// Không còn tham số giáo viên: lớp giờ là đơn vị hành chính của cả trung tâm, nên hai lớp
    /// trùng tên là hai thứ không ai phân biệt nổi lúc giao bài.
    /// </remarks>
    Task<bool> IsNameTakenAsync(
        string name,
        int? excludeGroupId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Các lớp trong danh sách id mà giáo viên này được phép giao bài — đã phân công và đã công bố.
    /// </summary>
    Task<IReadOnlyList<StudentGroup>> GetTeachableByIdsAsync(
        IEnumerable<int> groupIds,
        int teacherId,
        CancellationToken cancellationToken = default);

    /// <summary>Các giáo viên đang được phân công vào lớp.</summary>
    Task<IReadOnlyList<User>> GetAssignedTeachersAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>Mọi lớp giáo viên đang phụ trách, kể cả bản nháp, theo tên — cho màn quản lý giáo viên.</summary>
    Task<IReadOnlyList<StudentGroup>> GetByTeacherAsync(int teacherId, CancellationToken cancellationToken = default);

    /// <summary>Số lớp mỗi giáo viên đang phụ trách (kể cả bản nháp). Giáo viên không có lớp vắng mặt trong kết quả.</summary>
    Task<IReadOnlyDictionary<int, int>> CountByTeacherAsync(
        IEnumerable<int> teacherIds,
        CancellationToken cancellationToken = default);

    /// <summary>Các lớp một học sinh đang học, theo tên (kể cả lớp nháp và lớp đã khoá).</summary>
    Task<IReadOnlyList<StudentGroup>> GetByStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Sĩ số (số học sinh còn là thành viên) của từng lớp trong danh sách; lớp không có ai vẫn có mặt với 0.</summary>
    Task<IReadOnlyDictionary<int, int>> CountMembersAsync(IEnumerable<int> groupIds, CancellationToken cancellationToken = default);

    /// <summary>Trong các học sinh đã cho, những em đang là thành viên của lớp.</summary>
    Task<IReadOnlySet<int>> GetMemberIdsAsync(
        int groupId,
        IEnumerable<int> studentIds,
        CancellationToken cancellationToken = default);

    /// <summary>Dòng thành viên (được theo dõi, để xoá). <c>null</c> khi em không học lớp đó.</summary>
    Task<StudentGroupMember?> GetMemberAsync(int groupId, int studentId, CancellationToken cancellationToken = default);

    /// <summary>Mọi dòng thành viên của một học sinh (được theo dõi) — để gỡ em khỏi mọi lớp khi ẩn danh hoá.</summary>
    Task<IReadOnlyList<StudentGroupMember>> GetMembershipsOfStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    void AddMember(StudentGroupMember member);

    void RemoveMember(StudentGroupMember member);

    void RemoveMembers(IEnumerable<StudentGroupMember> members);

    void AddTeacher(StudentGroupTeacher assignment);

    void RemoveTeacher(StudentGroupTeacher assignment);

    /// <summary>
    /// Khoá dòng lớp tới hết transaction, để hai quản trị viên cùng đổi giáo viên của một lớp phải xếp hàng. Trả
    /// <c>false</c> khi lớp không tồn tại.
    /// </summary>
    Task<bool> LockForUpdateAsync(int groupId, CancellationToken cancellationToken = default);
}
