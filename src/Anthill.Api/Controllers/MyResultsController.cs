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
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IResultService _resultService;
    private readonly IGradeSheetService _gradeSheets;

    public MyResultsController(IResultService resultService, IGradeSheetService gradeSheets)
    {
        _resultService = resultService;
        _gradeSheets = gradeSheets;
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

    /// <summary>Bảng điểm của chính mình (.xlsx): mọi bài đã nộp, mỗi bài một dòng.</summary>
    /// <remarks>
    /// Đi qua đúng cổng xem điểm của <c>GET /api/my/results</c>: bài giáo viên chưa cho xem điểm ghi "Chưa công bố" và không có số
    /// nào trong file. Danh tính lấy từ token; <c>termId</c> (tuỳ chọn) lọc theo kỳ như bảng điểm: <c>0</c> là lớp chưa phân kỳ.
    /// </remarks>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        var result = await _gradeSheets.ExportMyResultsAsync(termId, cancellationToken);

        return result.IsSuccess
            ? File(result.ValueOrThrow.Content, XlsxContentType, result.ValueOrThrow.FileName)
            : FromFailure(result);
    }
}
