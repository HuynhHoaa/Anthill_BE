using Anthill.Application.Common;
using Anthill.Application.DTOs.MyLearning;
using Anthill.Application.Interfaces.Services;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anthill.Api.Controllers;

/// <summary>
/// Việc học của chính học sinh đang đăng nhập: lớp em học và bảng điểm theo môn. Đường dẫn bắt đầu bằng <c>/api/my</c> nên không mang id
/// học sinh nào để sửa (§3 quy tắc ④); danh tính suy từ JWT.
/// </summary>
[Authorize(Roles = SystemRoles.Student)]
[Route("api/my")]
public sealed class MyLearningController : ApiControllerBase
{
    private readonly IMyLearningService _learning;

    public MyLearningController(IMyLearningService learning)
    {
        _learning = learning;
    }

    /// <summary>Các lớp đã công bố em đang học, kèm số bài theo trạng thái.</summary>
    [HttpGet("groups")]
    [ProducesResponseType<IReadOnlyList<MyGroupDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListGroups(CancellationToken cancellationToken)
        => FromResult(await _learning.ListMyGroupsAsync(cancellationToken));

    /// <summary>Một lớp của em. Trả 404 khi em không học lớp đó (không phân biệt với lớp không tồn tại).</summary>
    [HttpGet("groups/{id:int}")]
    [ProducesResponseType<MyGroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGroup(int id, CancellationToken cancellationToken)
        => FromResult(await _learning.GetMyGroupAsync(id, cancellationToken));

    /// <summary>Các kỳ em có lớp, để lọc danh sách lớp và bảng điểm theo kỳ.</summary>
    [HttpGet("terms")]
    [ProducesResponseType<IReadOnlyList<MyTermDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListTerms(CancellationToken cancellationToken)
        => FromResult(await _learning.ListMyTermsAsync(cancellationToken));

    /// <summary>
    /// Bảng điểm: các môn em có bài kèm điểm trung bình từng môn. <c>?termId=</c> chỉ lấy bài của lớp thuộc kỳ đó (<c>0</c> là lớp chưa
    /// phân kỳ); bỏ trống là mọi kỳ.
    /// </summary>
    [HttpGet("transcript")]
    [ProducesResponseType<TranscriptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTranscript([FromQuery] int? termId, CancellationToken cancellationToken)
        => FromResult(await _learning.GetMyTranscriptAsync(termId, cancellationToken));
}
