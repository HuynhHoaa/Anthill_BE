using Anthill.Application.Common;
using Anthill.Application.DTOs.StudentGroups;
using Anthill.Application.DTOs.Students;
using Anthill.Application.Interfaces.Services;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    private readonly IStudentGroupService _groupService;

    public StudentGroupsController(IStudentGroupService groupService)
    {
        _groupService = groupService;
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

    /// <summary>Tạo nhóm lớp. Chủ sở hữu lấy từ JWT.</summary>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPost]
    [ProducesResponseType<StudentGroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] SaveStudentGroupRequest request,
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
    /// Gồm cả học sinh đang thuộc nhóm khác — mỗi em chỉ ở một nhóm, nên thêm vào đây nghĩa là
    /// <i>chuyển</i> nhóm. Trường <c>group</c> trong mỗi bản ghi cho biết em đó đang ở đâu.
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

    /// <summary>Gỡ một học sinh khỏi nhóm.</summary>
    /// <remarks>Đặt <c>GroupId = NULL</c>; tài khoản học sinh giữ nguyên (§4.4).</remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpDelete("{id:int}/students/{studentId:int}")]
    [ProducesResponseType<ApiMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(
        int id,
        int studentId,
        CancellationToken cancellationToken)
        => FromResult(
            await _groupService.RemoveMemberAsync(id, studentId, cancellationToken),
            "Đã gỡ học sinh khỏi lớp.");

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

    /// <summary>Giáo viên có thể phân công vào lớp này.</summary>
    /// <remarks>
    /// Bao gồm cả tài khoản đang bị khoá, kèm trạng thái, để quản trị viên hiểu vì sao không phân
    /// công được thay vì đi tìm một cái tên đã biến mất khỏi danh sách.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpGet("{id:int}/assignable-teachers")]
    [ProducesResponseType<IReadOnlyList<GroupTeacherDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAssignableTeachers(int id, CancellationToken cancellationToken)
        => FromResult(await _groupService.ListAssignableTeachersAsync(id, cancellationToken));

    /// <summary>Phân công giáo viên phụ trách lớp, theo lô.</summary>
    /// <remarks>
    /// Báo cáo theo từng loại bị bỏ qua vì mỗi loại đòi một hành động khác nhau: người đã được
    /// phân công thì không cần làm gì, id không phải giáo viên là chọn nhầm, còn tài khoản bị khoá
    /// thì phải mở khoá trước.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPost("{id:int}/teachers")]
    [ProducesResponseType<AssignTeachersResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignTeachers(
        int id,
        [FromBody] AssignTeachersRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _groupService.AssignTeachersAsync(id, request, cancellationToken));

    /// <summary>Gỡ một giáo viên khỏi lớp.</summary>
    /// <remarks>
    /// Gỡ được cả khi lớp đang có bài kiểm tra chạy dở: bài đã giao gắn với học sinh chứ không gắn
    /// với giáo viên, nên chúng không bị ảnh hưởng.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpDelete("{id:int}/teachers/{teacherId:int}")]
    [ProducesResponseType<ApiMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignTeacher(
        int id,
        int teacherId,
        CancellationToken cancellationToken)
        => FromResult(
            await _groupService.UnassignTeacherAsync(id, teacherId, cancellationToken),
            "Đã gỡ giáo viên khỏi lớp.");
}
