using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.StudentGroups;
using EduQuiz.Application.DTOs.Students;

namespace EduQuiz.Application.Interfaces.Services;

/// <summary>Nhóm lớp và thành viên — §4.4. Giáo viên sở hữu, quản trị viên chỉ đọc.</summary>
/// <remarks>
/// Ràng buộc phải cài: chủ sở hữu lấy từ JWT, không nhận từ body (§4.4) · chỉ xoá được nhóm
/// <b>rỗng</b>, và xoá nhóm không kéo theo tài khoản học sinh · gỡ thành viên là đặt
/// <c>GroupId = NULL</c> · <see cref="AddMembersAsync"/> nhận <b>mảng</b> id trong một lần gọi,
/// thay cho việc frontend gọi lặp từng em · <c>studentCount</c> luôn đếm qua JOIN, không lưu cứng ·
/// đổi tên nhóm <b>không</b> phải lan xuống bản ghi học sinh — học sinh chỉ giữ khoá ngoại (§4.4).
/// <para>
/// <b>Phạm vi sở hữu.</b> Giáo viên chỉ thấy và chỉ sửa được nhóm của chính mình; chạm vào nhóm
/// của người khác trả <c>NotFound</c> chứ không phải <c>Forbidden</c> — trả 403 là xác nhận id đó
/// có thật, đủ để dò dần danh sách lớp của đồng nghiệp (§4.2). Quản trị viên đọc được tất cả
/// nhưng không sửa được gì ở đây.
/// </para>
/// </remarks>
public interface IStudentGroupService
{
    /// <summary>Danh sách nhóm kèm sĩ số. Giáo viên thấy nhóm của mình, quản trị viên thấy tất cả.</summary>
    /// <param name="status">
    /// <c>draft</c> · <c>published</c> · <c>all</c> hoặc bỏ trống. Chỉ có tác dụng với quản trị
    /// viên — giáo viên vốn không bao giờ nhận về bản nháp, nên lọc thêm là vô nghĩa.
    /// </param>
    Task<Result<IReadOnlyList<StudentGroupDto>>> ListAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default);

    Task<Result<StudentGroupDto>> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Tạo nhóm. Chủ sở hữu lấy từ JWT, không nhận từ body.</summary>
    Task<Result<StudentGroupDto>> CreateAsync(
        SaveStudentGroupRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Đổi tên và mô tả. Tên mới không lan xuống bản ghi học sinh — chúng chỉ giữ khoá ngoại.</summary>
    Task<Result<StudentGroupDto>> UpdateAsync(
        int id,
        SaveStudentGroupRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xoá nhóm <b>rỗng</b>. Còn thành viên thì trả <c>Conflict</c> kèm sĩ số, để giáo viên biết
    /// phải chuyển bao nhiêu em đi trước.
    /// </summary>
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Thành viên của nhóm.</summary>
    Task<Result<IReadOnlyList<StudentDto>>> ListMembersAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>Học sinh có thể thêm vào nhóm — mọi học sinh chưa thuộc nhóm này.</summary>
    Task<Result<IReadOnlyList<StudentDto>>> ListAssignableAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm nhiều học sinh trong một lần gọi. Id lạ hoặc đã ở sẵn trong nhóm không làm hỏng cả mẻ;
    /// chúng được đếm riêng trong <see cref="AddMembersResponse"/>.
    /// </summary>
    Task<Result<AddMembersResponse>> AddMembersAsync(
        int id,
        AddMembersRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Gỡ khỏi nhóm bằng cách đặt <c>GroupId = NULL</c>. Tài khoản học sinh giữ nguyên.</summary>
    /// <summary>
    /// Công bố một lớp còn ở dạng nháp.
    /// </summary>
    /// <remarks>
    /// Tách khỏi <see cref="UpdateAsync"/> có chủ đích: công bố là một hành động, không phải một
    /// trường dữ liệu. Cùng khuôn với việc xuất bản đề kiểm tra, để người dùng chỉ phải học một
    /// khái niệm.
    ///
    /// Trả <see cref="ErrorCode.Conflict"/> khi lớp đã công bố rồi.
    /// </remarks>
    Task<Result<StudentGroupDto>> PublishAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đưa một lớp đã công bố trở lại dạng nháp.
    /// </summary>
    /// <remarks>
    /// Tồn tại vì công bố nhầm là chuyện xảy ra, và không có đường lui thì lớp kẹt vĩnh viễn ở
    /// trạng thái sai. Gỡ công bố <b>không</b> đụng tới học sinh hay bài đã giao — nó chỉ ẩn lớp
    /// khỏi màn hình của giáo viên trong lúc quản trị viên sắp xếp lại.
    ///
    /// Trả <see cref="ErrorCode.Conflict"/> khi lớp vốn đang là nháp.
    /// </remarks>
    Task<Result<StudentGroupDto>> UnpublishAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Giáo viên có thể phân công vào lớp này — chưa được phân công sẵn.</summary>
    Task<Result<IReadOnlyList<GroupTeacherDto>>> ListAssignableTeachersAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phân công giáo viên vào lớp theo lô.
    /// </summary>
    /// <remarks>
    /// Báo cáo theo <b>từng loại bị bỏ qua</b> chứ không chỉ thành công hay thất bại: một id lạ
    /// lẫn trong danh sách không được làm hỏng cả mẻ, và người bấm nút cần biết ai vào được, ai
    /// không, vì lý do gì.
    /// </remarks>
    Task<Result<AssignTeachersResponse>> AssignTeachersAsync(
        int id,
        AssignTeachersRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Gỡ một giáo viên khỏi lớp.</summary>
    Task<Result> UnassignTeacherAsync(int id, int teacherId, CancellationToken cancellationToken = default);

    Task<Result> RemoveMemberAsync(int id, int studentId, CancellationToken cancellationToken = default);
}
