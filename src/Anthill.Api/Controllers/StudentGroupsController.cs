using Anthill.Application.Common;
using Anthill.Application.DTOs.ExamAssignments;
using Anthill.Application.DTOs.StudentGroups;
using Anthill.Application.DTOs.Students;
using Anthill.Application.Interfaces.Services;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Anthill.Api.Controllers;

/// <summary>
/// Chín endpoint quản lý nhóm lớp của §4.4.
/// </summary>
/// <remarks>
/// <para>
/// Phân quyền ngược với <see cref="StudentsController"/>: ở đây <b>giáo viên sở hữu</b>, quản trị
/// viên chỉ đọc. Hai action đọc mở cho cả hai vai trò, bảy action còn lại chỉ giáo viên.
/// </para>
/// <para>
/// Chủ sở hữu <b>không</b> xuất hiện trong đường dẫn hay body ở bất kỳ đâu — nó luôn suy từ JWT
/// (§4.4). Kiểm tra phạm vi nằm trong <c>StudentGroupService</c> và trả <b>404</b> khi nhóm thuộc
/// giáo viên khác, chứ không phải 403: trả 403 là xác nhận id đó có thật (§4.2).
/// </para>
/// </remarks>
[Authorize(Roles = SystemRoles.AdminOrTeacher)]
[Route("api/student-groups")]
public sealed class StudentGroupsController : ApiControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IStudentGroupService _groupService;
    private readonly IGradeSheetService _gradeSheets;

    public StudentGroupsController(IStudentGroupService groupService, IGradeSheetService gradeSheets)
    {
        _groupService = groupService;
        _gradeSheets = gradeSheets;
    }

    /// <summary>Danh sách nhóm kèm sĩ số.</summary>
    /// <remarks>
    /// Không phân trang, đúng như frontend đang gọi: một giáo viên dạy vài lớp, và màn hình giao
    /// bài cần trọn danh sách để đổ vào ô chọn.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<StudentGroupDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
        => FromResult(await _groupService.ListAsync(search, status, cancellationToken));

    /// <summary>Chi tiết một nhóm.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<StudentGroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
        => FromResult(await _groupService.GetAsync(id, cancellationToken));

    /// <summary>Lập lớp mới, kèm giáo viên phụ trách.</summary>
    /// <remarks>
    /// Người lập lớp lấy từ JWT. Mỗi lớp có đúng một giáo viên, chọn ngay lúc lập: thiếu <c>teacherId</c>, hoặc giáo viên
    /// không có thật, trả <b>400</b>; tài khoản giáo viên đang bị khoá trả <b>409</b>. Lớp ra đời ở dạng nháp.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPost]
    [ProducesResponseType<StudentGroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStudentGroupRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _groupService.CreateAsync(request, cancellationToken));

    /// <summary>Đổi tên và mô tả nhóm.</summary>
    /// <remarks>
    /// Tên mới có hiệu lực ngay ở mọi màn hình mà không phải ghi lại bản ghi học sinh nào — học
    /// sinh chỉ giữ khoá ngoại <c>GroupId</c>, tên luôn đọc qua JOIN (§4.4).
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPut("{id:int}")]
    [ProducesResponseType<StudentGroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] SaveStudentGroupRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _groupService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Xoá một nhóm <b>rỗng</b>.</summary>
    /// <remarks>
    /// Nhóm còn thành viên trả <b>409</b> kèm sĩ số. Xoá nhóm không bao giờ kéo theo tài khoản học
    /// sinh (§4.4).
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType<ApiMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => FromResult(await _groupService.DeleteAsync(id, cancellationToken), "Đã xoá nhóm lớp.");

    /// <summary>Thành viên của nhóm.</summary>
    [Authorize(Roles = SystemRoles.AdminOrTeacher)]
    [HttpGet("{id:int}/students")]
    [ProducesResponseType<IReadOnlyList<StudentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMembers(int id, CancellationToken cancellationToken)
        => FromResult(await _groupService.ListMembersAsync(id, cancellationToken));

    /// <summary>Học sinh có thể thêm vào nhóm.</summary>
    /// <remarks>
    /// Mọi em chưa học lớp này, kể cả em đang học lớp khác — một học sinh học được nhiều lớp cùng lúc, nên thêm vào đây
    /// không kéo em ra khỏi lớp nào. Trường <c>groups</c> trong mỗi bản ghi cho biết các lớp em đang học.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpGet("{id:int}/assignable-students")]
    [ProducesResponseType<IReadOnlyList<StudentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAssignable(int id, CancellationToken cancellationToken)
        => FromResult(await _groupService.ListAssignableAsync(id, cancellationToken));

    /// <summary>Thêm <b>nhiều</b> học sinh vào nhóm trong một lần gọi.</summary>
    /// <remarks>
    /// Trả 200 kèm bảng phân loại (<c>added</c>, <c>skippedAlreadyMember</c>,
    /// <c>skippedNotFound</c>) thay vì làm hỏng cả mẻ vì một id lạc — xem
    /// <see cref="AddMembersResponse"/>.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPost("{id:int}/students")]
    [ProducesResponseType<AddMembersResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMembers(
        int id,
        [FromBody] AddMembersRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _groupService.AddMembersAsync(id, request, cancellationToken));

    /// <summary>Gỡ một học sinh khỏi nhóm, kèm lý do bắt buộc.</summary>
    /// <remarks>
    /// Xoá dòng thành viên của lớp này; tài khoản học sinh và các lớp khác của em giữ nguyên (§4.4). Lý do nằm trong body
    /// (10–512 ký tự) và được lưu vào lịch sử của học sinh. Bài lớp này giao mà em chưa bắt đầu và chưa hết hạn bị thu hồi
    /// khỏi em. Em đã làm bài vẫn gỡ được. Lớp đã khoá thì <b>409</b>.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpDelete("{id:int}/students/{studentId:int}")]
    [ProducesResponseType<MemberChangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(
        int id,
        int studentId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RemoveMemberRequest? request,
        CancellationToken cancellationToken)
    {
        // Không gửi thân request thì không có chỗ nào để validator nói "thiếu lý do", và mặc định ASP.NET trả 415 —
        // đúng về HTTP nhưng không giúp người gọi hiểu mình quên gì. Trả 400 kèm câu nói thẳng.
        if (request is null)
        {
            return FromFailure(Result.Failure("Vui lòng nhập lý do gỡ học sinh khỏi lớp."));
        }

        return FromResult(await _groupService.RemoveMemberAsync(id, studentId, request, cancellationToken));
    }

    /// <summary>Chuyển một học sinh từ lớp này sang lớp khác, kèm lý do bắt buộc.</summary>
    /// <remarks>
    /// Gỡ khỏi lớp {id} và xếp vào lớp đích trong một bước (một em học được nhiều lớp cùng lúc nên không còn khoá "đã làm
    /// bài thì không chuyển"). <b>409</b> khi em đã học lớp đích hoặc một trong hai lớp đã khoá.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPost("{id:int}/students/{studentId:int}/transfer")]
    [ProducesResponseType<MemberChangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TransferMember(
        int id,
        int studentId,
        [FromBody] TransferMemberRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _groupService.TransferMemberAsync(id, studentId, request, cancellationToken));

    /// <summary>Công bố một lớp còn ở dạng nháp.</summary>
    /// <remarks>
    /// Là một <b>hành động</b> riêng chứ không phải một trường trong payload sửa lớp — cùng khuôn
    /// với việc xuất bản đề kiểm tra. Trả <b>409</b> khi lớp đã công bố rồi.
    ///
    /// Cố ý không đòi lớp phải có sẵn học sinh hay giáo viên: một lớp đầu năm chưa xếp em nào vẫn
    /// là lớp có thật. Giao diện cảnh báo, server không cấm.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPost("{id:int}/publish")]
    [ProducesResponseType<StudentGroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(int id, CancellationToken cancellationToken)
        => FromResult(await _groupService.PublishAsync(id, cancellationToken));

    /// <summary>Đưa một lớp đã công bố trở lại dạng nháp.</summary>
    /// <remarks>
    /// Đường lui cho việc công bố nhầm. Không đụng tới học sinh hay bài đã giao — chỉ ẩn lớp khỏi
    /// màn hình giáo viên trong lúc quản trị viên sắp xếp lại. Trả <b>409</b> khi lớp vốn là nháp.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPost("{id:int}/unpublish")]
    [ProducesResponseType<StudentGroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unpublish(int id, CancellationToken cancellationToken)
        => FromResult(await _groupService.UnpublishAsync(id, cancellationToken));

    /// <summary>Khoá một lớp, kèm lý do bắt buộc.</summary>
    /// <remarks>
    /// Lớp khoá là lớp chỉ đọc: không xếp thêm em, không giao bài mới, không đổi giáo viên hay sửa lớp. Thành viên, bài đã giao
    /// và bảng điểm giữ nguyên, các em trong lớp vẫn học các lớp khác. Trả <b>409</b> khi lớp đã khoá rồi.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPost("{id:int}/lock")]
    [ProducesResponseType<StudentGroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Lock(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] LockGroupRequest? request,
        CancellationToken cancellationToken)
    {
        // Cùng lý do với RemoveMember: thiếu thân thì không có chỗ nào để validator nói "thiếu lý do".
        if (request is null)
        {
            return FromFailure(Result.Failure("Vui lòng nhập lý do khoá lớp."));
        }

        return FromResult(await _groupService.LockAsync(id, request, cancellationToken));
    }

    /// <summary>Mở khoá một lớp. Không cần lý do. Trả <b>409</b> khi lớp không bị khoá.</summary>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPost("{id:int}/unlock")]
    [ProducesResponseType<StudentGroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unlock(int id, CancellationToken cancellationToken)
        => FromResult(await _groupService.UnlockAsync(id, cancellationToken));

    /// <summary>Mọi giáo viên, để chọn người phụ trách lớp.</summary>
    /// <remarks>
    /// Bao gồm cả tài khoản đang bị khoá, kèm trạng thái, để quản trị viên hiểu vì sao không chọn được thay vì đi tìm một
    /// cái tên đã biến mất khỏi danh sách.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpGet("assignable-teachers")]
    [ProducesResponseType<IReadOnlyList<GroupTeacherDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTeachers(CancellationToken cancellationToken)
        => FromResult(await _groupService.ListTeachersAsync(cancellationToken));

    /// <summary>Đổi giáo viên phụ trách lớp.</summary>
    /// <remarks>
    /// Mỗi lớp có đúng một giáo viên, nên đây là <b>thay</b> chứ không thêm: lớp cũ còn nhiều giáo viên thì lần đổi này dọn
    /// về một. Đổi sang người khác giữa chừng thì <b>bắt buộc <c>reason</c></b> (10–512 ký tự, thiếu trả <b>400</b>) và được
    /// ghi vào lịch sử của lớp. Bài đã giao gắn với học sinh chứ không gắn với giáo viên, nên không bị ảnh hưởng. Tài khoản
    /// giáo viên đang bị khoá trả <b>409</b>.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPut("{id:int}/teacher")]
    [ProducesResponseType<StudentGroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeTeacher(
        int id,
        [FromBody] ChangeTeacherRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _groupService.ChangeTeacherAsync(id, request, cancellationToken));

    /// <summary>Lịch sử của lớp: lập lớp và đổi giáo viên phụ trách, kèm lý do.</summary>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpGet("{id:int}/history")]
    [ProducesResponseType<IReadOnlyList<GroupChangeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListHistory(int id, CancellationToken cancellationToken)
        => FromResult(await _groupService.ListChangesAsync(id, cancellationToken));

    /// <summary>Các bài kiểm tra đã giao cho lớp, mới nhất trước.</summary>
    /// <remarks>
    /// Lịch sử học của lớp: số em đã giao, đã làm và điểm trung bình của từng đề. Quản trị viên đọc mọi lớp; giáo viên đọc lớp đã
    /// công bố mình dạy (lớp khác trả 404) và thấy cả đề do giáo viên trước của lớp soạn.
    /// </remarks>
    [HttpGet("{id:int}/exams")]
    [ProducesResponseType<IReadOnlyList<GroupExamDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListExams(int id, CancellationToken cancellationToken)
        => FromResult(await _groupService.ListExamsAsync(id, cancellationToken));

    /// <summary>Từng em của lớp đã làm một bài kiểm tra ra sao: trạng thái, điểm, giờ bắt đầu và nộp, mã lượt làm bài.</summary>
    /// <remarks>
    /// Chỉ những em được giao bài qua lớp này. Quản trị viên đọc mọi lớp; giáo viên đọc lớp đã công bố mình dạy và thấy cả bài do giáo
    /// viên trước của lớp soạn (lớp khác trả 404). Từ <c>attemptId</c> mở bài làm bằng <c>GET /api/results/{attemptId}</c>, vốn cũng
    /// cho giáo viên đang dạy lớp đã giao bài đó đọc.
    /// </remarks>
    [HttpGet("{id:int}/exams/{examId:int}/results")]
    [ProducesResponseType<IReadOnlyList<ExamAssignmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListExamResults(int id, int examId, CancellationToken cancellationToken)
        => FromResult(await _groupService.ListExamResultsAsync(id, examId, cancellationToken));

    /// <summary>Bảng điểm của lớp (.xlsx): mỗi học sinh một dòng, mỗi đề đã giao cho lớp một cột.</summary>
    /// <remarks>
    /// Chỉ <b>giáo viên đang dạy lớp đã công bố</b> xuất được; quản trị viên nhận 403, lớp của người khác hoặc còn là bản nháp nhận
    /// 404. Có mọi đề đã giao cho lớp (kể cả đề của giáo viên trước), điểm quy về thang 10, ô trống là em chưa làm. Lớp đã khoá vẫn xuất được.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Teacher)]
    [HttpGet("{id:int}/grade-sheet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportGradeSheet(int id, CancellationToken cancellationToken)
    {
        var result = await _gradeSheets.ExportGroupAsync(id, cancellationToken);

        return result.IsSuccess
            ? File(result.ValueOrThrow.Content, XlsxContentType, result.ValueOrThrow.FileName)
            : FromFailure(result);
    }
}
