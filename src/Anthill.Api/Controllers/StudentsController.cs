using Anthill.Application.Common;
using Anthill.Application.DTOs.Students;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Mappings;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anthill.Api.Controllers;

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
        if (!StudentMappingExtensions.TryParseSettableStatus(request.Status, out var status))
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

    /// <summary>Đưa tài khoản học sinh vào danh sách chờ xoá, kèm lý do bắt buộc.</summary>
    /// <remarks>
    /// <para>
    /// <b>Không xoá ngay.</b> Sau 60 ngày, tiến trình nền mới xoá sạch dữ liệu cá nhân. Khoảng chờ
    /// đó tồn tại vì xoá người là thao tác không hoàn tác được, mà quyết định sai thường chỉ lộ ra
    /// sau vài tuần — "em đó chuyển trường" hoá ra nhầm với em khác cùng họ.
    /// </para>
    /// <para>
    /// Sau khi hết hạn, <b>dòng dữ liệu vẫn ở lại</b> nhưng tên, email, số điện thoại bị xoá; lượt
    /// làm bài và điểm giữ nguyên dưới tên "Học sinh đã xoá". Đó là cách để bảng điểm giáo viên đã
    /// dựng không thủng một dòng trong khi dữ liệu cá nhân vẫn biến mất thật.
    /// </para>
    /// <para>
    /// Token đang cầm chết ngay lập tức, giống nhánh khoá tài khoản.
    /// </para>
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPost("{id:int}/request-deletion")]
    [ProducesResponseType<StudentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestDeletion(
        int id,
        [FromBody] RequestDeletionRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _studentService.RequestDeletionAsync(id, request, cancellationToken));

    /// <summary>Đưa tài khoản đang chờ xoá trở lại hoạt động.</summary>
    /// <remarks>
    /// Chỉ dùng được trong thời gian chờ. Sau khi dữ liệu đã bị xoá thì trả <b>409</b> — không còn
    /// gì để khôi phục, và nói thẳng điều đó tử tế hơn là trả về thành công trên một tài khoản rỗng.
    /// </remarks>
    [Authorize(Roles = SystemRoles.Admin)]
    [HttpPost("{id:int}/restore")]
    [ProducesResponseType<StudentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
        => FromResult(await _studentService.RestoreAsync(id, cancellationToken));
}
