using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.Students;
using EduQuiz.Application.Interfaces.Services;
using EduQuiz.Application.Mappings;
using EduQuiz.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduQuiz.Api.Controllers;

/// <summary>
/// Bảy endpoint quản lý tài khoản học sinh của §4.3.
/// </summary>
/// <remarks>
/// <b>Phân quyền là phần đáng đọc nhất ở file này.</b> §4.2 chia rõ: quản trị viên <i>sở hữu tài
/// khoản</i> — tạo, sửa, khoá, reset mật khẩu; giáo viên chỉ <i>đọc</i>, vì họ cần danh sách lớp
/// để giao bài chứ không cần quyền đổi mật khẩu của học sinh. Vì vậy lớp mặc định là
/// <c>[Authorize(Roles = AdminOrTeacher)]</c>, còn năm action ghi đè thành chỉ quản trị viên.
/// <para>
/// Controller không chứa logic nghiệp vụ: mỗi action gọi đúng một phương thức của
/// <see cref="IStudentService"/> rồi để <see cref="ApiControllerBase"/> dịch kết quả sang HTTP.
/// </para>
/// </remarks>
[Authorize(Roles = SystemRoles.AdminOrTeacher)]
[Route("api/students")]
public sealed class StudentsController : ApiControllerBase
{
    private readonly IStudentService _studentService;

    public StudentsController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    /// <summary>Danh sách học sinh, có phân trang và lọc.</summary>
    /// <remarks>
    /// <c>page</c> và <c>pageSize</c> được <b>kẹp</b> chứ không báo lỗi, và response nói rõ số trang
    /// thực tế đã dùng để frontend đồng bộ ngược lên URL (§5).
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<StudentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] ListStudentsQuery query,
        CancellationToken cancellationToken)
        => FromResult(await _studentService.ListAsync(query, cancellationToken));

    /// <summary>
    /// Kiểm tra email / tên đăng nhập / mã học sinh đã có ai dùng chưa.
    /// </summary>
    /// <remarks>
    /// Phải khai báo <b>trước</b> <c>GET /{id}</c>: đặt sau thì "availability" bị khớp vào tham số
    /// <c>{id}</c> và request chết ở bước bind model với lỗi 400 khó hiểu.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpGet("availability")]
    [ProducesResponseType<AvailabilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckAvailability(
        [FromQuery] AvailabilityQuery query,
        CancellationToken cancellationToken)
        => FromResult(await _studentService.CheckAvailabilityAsync(query, cancellationToken));

    /// <summary>Hồ sơ một học sinh.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<StudentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
        => FromResult(await _studentService.GetAsync(id, cancellationToken));

    /// <summary>Tạo tài khoản học sinh kèm mật khẩu ban đầu.</summary>
    /// <remarks>
    /// Trùng email / tên đăng nhập / mã học sinh trả <b>409</b>, không phải 400 (§5) — đó là xung
    /// đột với dữ liệu đang có, không phải lỗi hình dạng request.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPost]
    [ProducesResponseType<StudentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStudentRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _studentService.CreateAsync(request, cancellationToken));

    /// <summary>Cập nhật hồ sơ, nhóm lớp và trạng thái.</summary>
    /// <remarks><b>Không nhận mật khẩu</b> (§4.3) — việc đó thuộc về endpoint reset-password.</remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPut("{id:int}")]
    [ProducesResponseType<StudentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateStudentRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _studentService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Khoá hoặc mở khoá tài khoản.</summary>
    /// <remarks>
    /// Khoá sẽ xoay <c>SecurityStamp</c>, nên mọi token đã cấp cho học sinh đó mất hiệu lực ngay
    /// lập tức — kể cả khi em đang mở màn hình làm bài (§5).
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<StudentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        int id,
        [FromBody] UpdateStudentStatusRequest request,
        CancellationToken cancellationToken)
    {
        // Validator đã bảo đảm chuỗi đọc được trước khi action chạy, nên nhánh false ở đây không
        // với tới được. Vẫn giữ để controller không phải dùng dấu chấm than: một ngày nào đó
        // validator bị gỡ khỏi pipeline thì đây trả 400 chứ không ném NullReference.
        if (!StudentMappingExtensions.TryParseStatus(request.Status, out var status))
        {
            return FromFailure(Result.Failure("Trạng thái tài khoản chỉ nhận giá trị 'active' hoặc 'locked'."));
        }

        return FromResult(await _studentService.SetStatusAsync(id, status, cancellationToken));
    }

    /// <summary>Cấp lại mật khẩu cho học sinh quên mật khẩu.</summary>
    /// <remarks>
    /// Response mang mật khẩu mới dưới dạng đọc được — <b>đúng một lần duy nhất</b>. Database chỉ
    /// giữ chuỗi băm, nên đóng cửa sổ mà chưa chép lại thì phải reset lần nữa.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPost("{id:int}/reset-password")]
    [ProducesResponseType<ResetPasswordResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(int id, CancellationToken cancellationToken)
        => FromResult(await _studentService.ResetPasswordAsync(id, cancellationToken));
}
