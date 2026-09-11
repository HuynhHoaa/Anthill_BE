using Anthill.Application.Common;
using Anthill.Application.DTOs.Dashboard;
using Anthill.Application.Interfaces.Services;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anthill.Api.Controllers;

/// <summary>Hai endpoint tổng quan gộp — §4.8.</summary>
/// <remarks>
/// Mỗi trang tổng quan là <b>một</b> lời gọi. Tách thành sáu endpoint nhỏ nghe sạch hơn về mặt
/// thiết kế, nhưng đó là sáu lần chịu độ trễ cho một màn hình; trên dịch vụ miễn phí có cold start
/// thì cả sáu cùng đánh thức tiến trình và người dùng ngồi chờ sáu lần.
/// </remarks>
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>Tổng quan của giáo viên: bảy con số và vài đề gần đây.</summary>
    /// <remarks>Phạm vi là dữ liệu của chính giáo viên đó — học sinh trong lớp mình, đề mình tạo.</remarks>
    [HttpGet("teacher")]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<TeacherDashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTeacherDashboard(CancellationToken cancellationToken)
        => FromResult(await _dashboardService.GetTeacherDashboardAsync(cancellationToken));

    /// <summary>Tổng quan của học sinh: bốn con số, bài đang mở, bài sắp tới, kết quả gần nhất.</summary>
    /// <remarks>
    /// Điểm trung bình chỉ tính trên những bài giáo viên đã cho xem điểm. Gộp cả điểm đang giấu vào
    /// một con số trung bình là để lộ nó bằng phép trừ.
    /// </remarks>
    [HttpGet("student")]
    [Authorize(Roles = SystemRoles.Student)]
    [ProducesResponseType<StudentDashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStudentDashboard(CancellationToken cancellationToken)
        => FromResult(await _dashboardService.GetStudentDashboardAsync(cancellationToken));
}
