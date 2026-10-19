using Anthill.Application.Common;
using Anthill.Application.DTOs.Accounts;
using Anthill.Application.DTOs.Teachers;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Mappings;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anthill.Api.Controllers;

/// <summary>
/// Quản lý tài khoản giáo viên — chỉ quản trị viên (nhà trường cấp tài khoản, gán môn, khoá khi giáo viên nghỉ).
/// </summary>
/// <remarks>
/// Không có xoá: giáo viên sở hữu câu hỏi, đề và lịch sử giao bài. Nghỉ việc thì khoá tài khoản rồi đổi giáo viên phụ trách
/// của các lớp họ đang dạy.
/// </remarks>
[Authorize(Roles = SystemRoles.Admin)]
[Route("api/teachers")]
public sealed class TeachersController : ApiControllerBase
{
    private readonly ITeacherService _teacherService;

    public TeachersController(ITeacherService teacherService)
    {
        _teacherService = teacherService;
    }

    /// <summary>Danh sách giáo viên, có phân trang, tìm kiếm và lọc theo trạng thái, môn.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<TeacherDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] ListTeachersQuery query, CancellationToken cancellationToken)
        => FromResult(await _teacherService.ListAsync(query, cancellationToken));

    /// <summary>Một giáo viên kèm các lớp đang phụ trách.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<TeacherDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
        => FromResult(await _teacherService.GetAsync(id, cancellationToken));

    /// <summary>Tạo tài khoản giáo viên kèm mật khẩu ban đầu và môn giảng dạy.</summary>
    /// <remarks>
    /// Email là tên đăng nhập. Mật khẩu do quản trị viên cấp nên giáo viên bị ép đổi ở lần đăng nhập đầu. Email trùng bất kỳ
    /// định danh nào đang có (email, tên đăng nhập, mã) trả <b>409</b>; môn không có thật trả <b>400</b>.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<TeacherDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateTeacherRequest request, CancellationToken cancellationToken)
        => FromResult(await _teacherService.CreateAsync(request, cancellationToken));

    /// <summary>Sửa họ tên, email, số điện thoại và môn.</summary>
    /// <remarks>Đổi môn trả <b>409</b> khi giáo viên đã soạn câu hỏi hoặc đề: chúng thuộc môn cũ.</remarks>
    [HttpPut("{id:int}")]
    [ProducesResponseType<TeacherDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateTeacherRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _teacherService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Khoá hoặc mở khoá tài khoản giáo viên.</summary>
    /// <remarks>
    /// Khoá bắt buộc <c>reason</c> (10–512 ký tự) và xoay <c>SecurityStamp</c>: mọi token đã cấp mất hiệu lực ngay. Các lớp
    /// giáo viên đang phụ trách vẫn giữ tên họ cho tới khi quản trị viên đổi người.
    /// </remarks>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<TeacherDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        int id,
        [FromBody] UpdateTeacherStatusRequest request,
        CancellationToken cancellationToken)
    {
        // Validator đã bảo đảm chuỗi đọc được; nhánh false giữ để controller khỏi dùng dấu chấm than.
        if (!StudentMappingExtensions.TryParseSettableStatus(request.Status, out var status))
        {
            return FromFailure(Result.Failure("Trạng thái tài khoản chỉ nhận giá trị 'active' hoặc 'locked'."));
        }

        return FromResult(await _teacherService.SetStatusAsync(id, status, request.Reason, cancellationToken));
    }

    /// <summary>Cấp lại mật khẩu cho giáo viên quên mật khẩu.</summary>
    /// <remarks>Mật khẩu mới ở dạng đọc được <b>đúng một lần</b>; database chỉ giữ chuỗi băm.</remarks>
    [HttpPost("{id:int}/reset-password")]
    [ProducesResponseType<TeacherPasswordResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(int id, CancellationToken cancellationToken)
        => FromResult(await _teacherService.ResetPasswordAsync(id, cancellationToken));

    /// <summary>Lịch sử khoá và mở khoá của một giáo viên, kèm lý do.</summary>
    [HttpGet("{id:int}/history")]
    [ProducesResponseType<IReadOnlyList<AccountChangeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListHistory(int id, CancellationToken cancellationToken)
        => FromResult(await _teacherService.ListChangesAsync(id, cancellationToken));
}
