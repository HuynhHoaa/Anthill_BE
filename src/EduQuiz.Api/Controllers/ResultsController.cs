using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.Results;
using EduQuiz.Application.Interfaces.Services;
using EduQuiz.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduQuiz.Api.Controllers;

/// <summary>Bảng kết quả và chi tiết bài làm — §4.8.</summary>
/// <remarks>
/// <para>
/// Bốn action đầu thuộc về giáo viên. Riêng <c>GET /{attemptId}</c> mở cho <b>cả học sinh</b>, vì
/// đó là màn hình xem lại bài của chính mình — nhưng thứ trả về khác nhau: quyền xem đáp án tính ở
/// server rồi cắt hẳn khỏi JSON, không phải gửi đi rồi nhờ giao diện giấu (§3 quy tắc ⑥).
/// </para>
/// <para>
/// Bài của người khác trả <b>404</b>, không phải 403 — 403 là xác nhận id đó có thật, đủ để dò dần
/// bảng kết quả của cả lớp (§4.2).
/// </para>
/// </remarks>
[Authorize]
[Route("api/results")]
public sealed class ResultsController : ApiControllerBase
{
    private readonly IResultService _resultService;

    public ResultsController(IResultService resultService)
    {
        _resultService = resultService;
    }

    /// <summary>Bảng kết quả, lọc chéo theo đề / học sinh / nhóm / trạng thái.</summary>
    [HttpGet]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<PagedResult<ResultSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] ListResultsQuery query,
        CancellationToken cancellationToken)
        => FromResult(await _resultService.ListAsync(query, cancellationToken));

    /// <summary>Bốn ô số liệu đầu trang.</summary>
    /// <remarks>
    /// Nhận <b>cùng bộ lọc</b> với bảng bên dưới. Một màn hình mà bốn ô nói về toàn hệ thống còn
    /// bảng ngay dưới đã lọc là một màn hình gây hiểu sai — và frontend phải dán thêm chữ
    /// "toàn hệ thống" để chữa, thay vì số liệu tự đúng.
    /// </remarks>
    [HttpGet("stats")]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<ResultStatsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStats(
        [FromQuery] ListResultsQuery query,
        CancellationToken cancellationToken)
        => FromResult(await _resultService.GetStatsAsync(query, cancellationToken));

    /// <summary>Chi tiết một lượt làm bài, soi từng câu.</summary>
    /// <remarks>
    /// Câu học sinh <b>bỏ trống vẫn xuất hiện</b> với <c>selectedAnswerId</c> để trống — đó thường
    /// là những câu đáng xem nhất. Trường <c>correctAnswerId</c> và <c>isCorrect</c> chỉ có mặt khi
    /// người gọi đủ điều kiện xem đáp án.
    /// </remarks>
    [HttpGet("{attemptId:int}")]
    [Authorize(Roles = SystemRoles.All)]
    [ProducesResponseType<AttemptDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(int attemptId, CancellationToken cancellationToken)
        => FromResult(await _resultService.GetDetailAsync(attemptId, cancellationToken));

    /// <summary>Tóm tắt một lượt — cho màn hình vừa nộp bài xong.</summary>
    /// <remarks>
    /// Tồn tại riêng vì màn hình đó chỉ cần một vòng tròn điểm, mà trước đây nó gọi endpoint chi
    /// tiết và kéo về toàn bộ câu hỏi kèm đáp án chỉ để hiện một con số (§4.8).
    /// </remarks>
    [HttpGet("{attemptId:int}/summary")]
    [Authorize(Roles = SystemRoles.All)]
    [ProducesResponseType<ResultSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary(int attemptId, CancellationToken cancellationToken)
        => FromResult(await _resultService.GetSummaryAsync(attemptId, cancellationToken));
}
