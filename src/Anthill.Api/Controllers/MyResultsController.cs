using Anthill.Application.Common;
using Anthill.Application.DTOs.Results;
using Anthill.Application.Interfaces.Services;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anthill.Api.Controllers;

/// <summary>Lịch sử làm bài của chính học sinh đang đăng nhập — §4.8.</summary>
/// <remarks>
/// Đường dẫn <c>/api/my/...</c> nói rõ phạm vi ngay ở URL: không có tham số <c>studentId</c> nào,
/// danh tính lấy từ token (§3 quy tắc ④). Cùng khuôn với <c>/api/my/exams</c> của bước 6.
/// </remarks>
[Authorize(Roles = SystemRoles.Student)]
[Route("api/my/results")]
public sealed class MyResultsController : ApiControllerBase
{
    private readonly IResultService _resultService;

    public MyResultsController(IResultService resultService)
    {
        _resultService = resultService;
    }

    /// <summary>Các bài đã làm của chính mình. Điểm chỉ có mặt khi giáo viên cho xem.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ResultSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] ListMyResultsQuery query,
        CancellationToken cancellationToken)
        => FromResult(await _resultService.ListMyResultsAsync(query, cancellationToken));
}
