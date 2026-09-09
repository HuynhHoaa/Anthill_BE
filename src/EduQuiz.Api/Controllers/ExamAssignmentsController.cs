using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.ExamAssignments;
using EduQuiz.Application.Interfaces.Services;
using EduQuiz.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduQuiz.Api.Controllers;

/// <summary>
/// Ba endpoint giao bài của §4.6, lồng dưới đường dẫn của đề.
/// </summary>
/// <remarks>
/// Tách khỏi <see cref="ExamsController"/> vì đây là một nghiệp vụ khác: kia quản lý <i>cấu hình</i>
/// của đề, còn đây quản lý <i>ai được làm đề đó</i>. Đường dẫn vẫn lồng dưới <c>/api/exams/{examId}</c>
/// để mọi thao tác đều phải đi qua một đề cụ thể — không có cách nào chạm tới một assignment mà
/// không nói rõ nó thuộc đề nào.
/// </remarks>
[Authorize(Roles = SystemRoles.AdminOrTeacher)]
[Route("api/exams/{examId:int}/assignments")]
public sealed class ExamAssignmentsController : ApiControllerBase
{
    private readonly IExamAssignmentService _assignmentService;

    public ExamAssignmentsController(IExamAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    /// <summary>Danh sách học sinh đã giao, kèm tiến trình làm bài của từng em.</summary>
    /// <remarks>
    /// Trạng thái từng em là dữ liệu dẫn xuất: chưa có lượt làm bài thì <c>assigned</c>, có rồi thì
    /// lấy trạng thái của lượt đó, còn đề đã đóng mà em chưa từng bắt đầu thì <c>expired</c>.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ExamAssignmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(int examId, CancellationToken cancellationToken)
        => FromResult(await _assignmentService.ListAsync(examId, cancellationToken));

    /// <summary>Giao bài cho học sinh lẻ và/hoặc cả nhóm lớp.</summary>
    /// <remarks>
    /// <para>
    /// <b>Idempotent</b> theo cặp (đề, học sinh): bấm hai lần không tạo hai bài, và không đụng tới
    /// trạng thái hay điểm của em đã làm rồi.
    /// </para>
    /// <para>
    /// Một lô hỏng một phần thì phần còn lại vẫn chạy, nhưng những gì bị bỏ qua đều được đếm và
    /// trả về — tài khoản đang khoá, học sinh không thuộc lớp của mình, id không tồn tại. Im lặng
    /// ở đây nghĩa là tới hôm thi mới phát hiện thiếu bài.
    /// </para>
    /// <para>Trả <b>409</b> khi đề còn là bản nháp; frontend hoàn toàn không kiểm điều này.</para>
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<AssignExamResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assign(
        int examId,
        [FromBody] AssignExamRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _assignmentService.AssignAsync(examId, request, cancellationToken));

    /// <summary>Thu hồi một bài đã giao.</summary>
    /// <remarks>
    /// Bổ sung so với frontend: giao nhầm lớp là chuyện thường, mà endpoint giao bài lại cố ý
    /// idempotent nên không có đường lùi. Trả <b>409</b> khi em đã bắt đầu làm bài — thu hồi lúc
    /// đó là xoá luôn phần bài em đã làm.
    /// </remarks>
    [HttpDelete("{assignmentId:int}")]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<ApiMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke(
        int examId,
        int assignmentId,
        CancellationToken cancellationToken)
        => FromResult(
            await _assignmentService.RevokeAsync(examId, assignmentId, cancellationToken),
            "Đã thu hồi bài kiểm tra của học sinh này.");
}
