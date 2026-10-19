using Anthill.Application.Common;
using Anthill.Application.DTOs.ExamAssignments;
using Anthill.Application.DTOs.StudentGroups;
using Anthill.Application.DTOs.Students;

namespace Anthill.Application.Interfaces.Services;

/// <summary>Nhóm lớp và thành viên — §4.4. Giáo viên sở hữu, quản trị viên chỉ đọc.</summary>
/// <remarks>
/// Ràng buộc phải cài: chủ sở hữu lấy từ JWT, không nhận từ body (§4.4) · chỉ xoá được nhóm
/// <b>rỗng</b>, và xoá nhóm không kéo theo tài khoản học sinh · gỡ thành viên là xoá dòng thành viên
/// (một em học được nhiều lớp cùng lúc) · <see cref="AddMembersAsync"/> nhận <b>mảng</b> id trong một lần gọi,
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

    /// <summary>Lập lớp mới kèm giáo viên phụ trách (bắt buộc). Người lập lấy từ JWT, không nhận từ body.</summary>
    Task<Result<StudentGroupDto>> CreateAsync(
        CreateStudentGroupRequest request,
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

    /// <summary>Học sinh có thể thêm vào nhóm — những em chưa học lớp này (kể cả em đang học lớp khác).</summary>
    Task<Result<IReadOnlyList<StudentDto>>> ListAssignableAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm nhiều học sinh <b>chưa có lớp</b> trong một lần gọi. Id lạ, em đã ở sẵn trong nhóm, em đang ở lớp
    /// khác hay em bị khoá lớp không làm hỏng cả mẻ; chúng được đếm riêng trong <see cref="AddMembersResponse"/>.
    /// </summary>
    /// <remarks>
    /// Không còn đường chuyển lớp ngầm: em đang ở lớp khác bị bỏ qua, vì đổi lớp phải đi qua
    /// <see cref="TransferMemberAsync"/> để có lý do và để luật khoá lớp được kiểm.
    /// </remarks>
    Task<Result<AddMembersResponse>> AddMembersAsync(
        int id,
        AddMembersRequest request,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Khoá lớp, kèm lý do bắt buộc (10–512 ký tự, ghi vào lịch sử lớp). Lớp khoá là lớp chỉ đọc: không xếp thêm em, không giao
    /// bài mới, không đổi giáo viên hay sửa lớp; thành viên, bài đã giao và bảng điểm giữ nguyên, và các em trong lớp vẫn học
    /// các lớp khác bình thường. Trả <see cref="ErrorCode.Conflict"/> khi lớp đã khoá.
    /// </summary>
    Task<Result<StudentGroupDto>> LockAsync(
        int id,
        LockGroupRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Mở khoá lớp (không cần lý do; việc mở khoá được ghi vào lịch sử lớp). Trả <c>Conflict</c> khi lớp không bị khoá.</summary>
    Task<Result<StudentGroupDto>> UnlockAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mọi giáo viên, kèm trạng thái tài khoản, cho ô chọn giáo viên phụ trách khi lập lớp và khi đổi người. Chỉ quản trị
    /// viên.
    /// </summary>
    Task<Result<IReadOnlyList<GroupTeacherDto>>> ListTeachersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Đổi giáo viên phụ trách: lớp có đúng một giáo viên nên đây là <b>thay</b> chứ không thêm. Đổi sang người khác giữa
    /// chừng thì <b>bắt buộc lý do</b> và được ghi vào lịch sử của lớp. Lớp cũ còn nhiều giáo viên thì lần đổi này dọn về một.
    /// Tài khoản đang bị khoá không nhận được lớp (409).
    /// </summary>
    Task<Result<StudentGroupDto>> ChangeTeacherAsync(
        int id,
        ChangeTeacherRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Lịch sử của lớp (lập lớp, đổi giáo viên kèm lý do), mới nhất trước. Chỉ quản trị viên.</summary>
    Task<Result<IReadOnlyList<GroupChangeDto>>> ListChangesAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Các bài kiểm tra đã giao cho lớp, mới nhất trước, kèm số em đã làm và điểm trung bình — lịch sử học của lớp. Quản trị
    /// viên đọc mọi lớp; giáo viên đọc lớp đã công bố mình dạy (lớp khác 404) và thấy cả đề của giáo viên trước.
    /// </summary>
    Task<Result<IReadOnlyList<GroupExamDto>>> ListExamsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Từng em của lớp đã làm một bài kiểm tra ra sao (trạng thái, điểm, giờ bắt đầu và nộp, mã lượt làm bài) — chỉ những em được
    /// giao bài <b>qua lớp này</b>. Quản trị viên đọc mọi lớp; giáo viên đọc lớp đã công bố mình dạy, <b>kể cả bài do giáo viên
    /// trước của lớp soạn</b> (người nhận lớp cần nắm sức học cả lớp). Lớp không đọc được trả 404.
    /// </summary>
    Task<Result<IReadOnlyList<ExamAssignmentDto>>> ListExamResultsAsync(
        int id,
        int examId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gỡ một em khỏi lớp bằng cách xoá dòng thành viên, kèm lý do bắt buộc. Tài khoản học sinh và các lớp khác của em giữ
    /// nguyên.
    /// </summary>
    /// <remarks>
    /// Gỡ được cả em đã làm bài (em nghỉ học, chuyển trường). Những bài lớp này giao mà em chưa bắt đầu và chưa hết hạn
    /// bị thu hồi khỏi em; bài đã bắt đầu và bài đã hết hạn được giữ — xem <see cref="MemberChangeResponse"/>. Lý do, người
    /// thực hiện và thời điểm được ghi vào lịch sử của học sinh. Lớp đã khoá thì không gỡ được (409).
    /// </remarks>
    Task<Result<MemberChangeResponse>> RemoveMemberAsync(
        int id,
        int studentId,
        RemoveMemberRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuyển một em từ lớp <paramref name="id"/> sang lớp khác, kèm lý do bắt buộc.
    /// </summary>
    /// <remarks>
    /// Chuyển là gỡ khỏi lớp này rồi xếp vào lớp đích trong một bước; không còn khoá "đã làm bài thì không chuyển" vì em học
    /// được nhiều lớp cùng lúc. Trả <see cref="ErrorCode.Conflict"/> khi em đã học lớp đích hoặc một trong hai lớp đã khoá.
    /// Bài lớp cũ giao mà em chưa làm bị thu hồi như khi gỡ khỏi lớp; em không tự nhận bài đã giao cho lớp mới — giáo viên
    /// giao lại lớp như với một em mới vào.
    /// </remarks>
    Task<Result<MemberChangeResponse>> TransferMemberAsync(
        int id,
        int studentId,
        TransferMemberRequest request,
        CancellationToken cancellationToken = default);
}
