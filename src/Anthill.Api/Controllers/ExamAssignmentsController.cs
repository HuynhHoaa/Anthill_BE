using Anthill.Application.Common;
using Anthill.Application.DTOs.ExamAssignments;
using Anthill.Application.Interfaces.Services;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anthill.Api.Controllers;

/// <summary>
/// Các endpoint giao bài theo lớp của §4.6, lồng dưới đường dẫn của đề.
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
    /// lấy trạng thái của lượt đó, còn khung giờ của em đã đóng (lịch riêng của lớp nếu có) mà em
    /// chưa từng bắt đầu thì <c>expired</c>. Mỗi dòng mang kèm khung giờ thật của em đó.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ExamAssignmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(int examId, CancellationToken cancellationToken)
        => FromResult(await _assignmentService.ListAsync(examId, cancellationToken));

    /// <summary>Giao bài cho một hay nhiều lớp.</summary>
    /// <remarks>
    /// <para>
    /// Chỉ giao theo lớp: request mang danh sách học sinh (<c>studentIds</c>) trả <b>400</b>, vì bài kiểm
    /// tra không giao riêng từng học sinh.
    /// </para>
    /// <para>
    /// <b>Idempotent</b> theo cặp (đề, học sinh): bấm hai lần không tạo hai bài, và không đụng tới
    /// trạng thái hay điểm của em đã làm rồi. Em mới vào lớp nhận bài khi gửi lại lớp.
    /// </para>
    /// <para>
    /// Một lô hỏng một phần thì phần còn lại vẫn chạy, nhưng những gì bị bỏ qua đều được đếm và
    /// trả về — tài khoản đang khoá, học sinh không thuộc lớp của mình, id không tồn tại. Im lặng
    /// ở đây nghĩa là tới hôm thi mới phát hiện thiếu bài.
    /// </para>
    /// <para>
    /// Gửi lại một lớp đã giao kèm khung giờ là cách sửa giờ cho lớp đó: những em được giao qua lớp
    /// ấy mà chưa bắt đầu làm bài được đổi sang khung mới (<c>scheduleUpdatedCount</c>). Bỏ trống
    /// khung giờ thì không khung giờ nào bị xoá.
    /// </para>
    /// <para>
    /// Trả <b>400</b> khi khung giờ riêng ngắn hơn thời lượng làm bài, <b>409</b> khi đề còn là bản
    /// nháp; frontend hoàn toàn không kiểm điều này.
    /// </para>
    /// <para>
    /// Gửi lại một lớp kèm khung giờ khác trả <b>409</b> nếu khung giờ lớp đang theo đã khoá (còn dưới 120
    /// phút nữa là mở cho tới hết giờ đóng) và còn em chưa bắt đầu bị đổi. Giao thêm em vào lớp, hoặc gửi lại
    /// đúng khung đang giữ, thì vẫn được.
    /// </para>
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

    /// <summary>Thu hồi bài đã giao cho cả một lớp.</summary>
    /// <remarks>
    /// <para>
    /// Gỡ mọi em của lớp chưa bắt đầu làm bài; em đã bắt đầu giữ nguyên bài (gỡ lúc đó là xoá luôn phần bài đã
    /// làm). Kết quả trả số em đã gỡ (<c>revokedCount</c>) và số em đã bắt đầu nên còn lại
    /// (<c>keptStartedCount</c>).
    /// </para>
    /// <para>
    /// Bổ sung so với frontend: giao nhầm lớp là chuyện thường, mà endpoint giao bài lại cố ý idempotent nên
    /// không có đường lùi. <b>404</b> khi lớp chưa được giao đề này; <b>409</b> khi mọi em của lớp đã bắt đầu
    /// làm bài nên không còn gì để gỡ.
    /// </para>
    /// </remarks>
    [HttpDelete("classes/{groupId:int}")]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<RevokeClassResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeClass(
        int examId,
        int groupId,
        CancellationToken cancellationToken)
        => FromResult(await _assignmentService.RevokeClassAsync(examId, groupId, cancellationToken));
}
