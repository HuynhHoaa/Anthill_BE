using Anthill.Application.Common;
using Anthill.Application.DTOs.Terms;
using Anthill.Application.Interfaces.Services;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anthill.Api.Controllers;

/// <summary>
/// Danh mục kỳ học — chỉ quản trị viên. Kỳ là nhãn gom lớp theo thời gian (học kỳ ở đại học, cấp lớp ở phổ thông); học sinh lọc lớp và
/// bảng điểm theo kỳ qua <c>GET /api/my/terms</c>.
/// </summary>
[Authorize(Roles = SystemRoles.Admin)]
[Route("api/terms")]
public sealed class TermsController : ApiControllerBase
{
    private readonly ITermService _terms;

    public TermsController(ITermService terms)
    {
        _terms = terms;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AcademicTermDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => FromResult(await _terms.ListAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType<AcademicTermDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] SaveAcademicTermRequest request, CancellationToken cancellationToken)
        => FromResult(await _terms.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    [ProducesResponseType<AcademicTermDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] SaveAcademicTermRequest request, CancellationToken cancellationToken)
        => FromResult(await _terms.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => FromResult(await _terms.DeleteAsync(id, cancellationToken), "Đã xoá kỳ học.");
}
