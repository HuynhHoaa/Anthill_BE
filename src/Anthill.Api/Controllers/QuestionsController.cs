using Anthill.Application.Common;
using Anthill.Application.DTOs.Questions;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Mappings;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anthill.Api.Controllers;

/// <summary>
/// Tám endpoint quản lý ngân hàng câu hỏi của §4.5.
/// </summary>
/// <remarks>
/// <para>
/// Khác hai controller trước, ở đây <b>không có action nào bị thu hẹp vai trò</b>: §4.5 cho cả
/// giáo viên lẫn quản trị viên đọc và ghi. Hàng rào thật không nằm ở vai trò mà ở <b>phạm vi</b> —
/// <c>SubjectId</c> quyết định ai thấy câu nào, <c>CreatedByUserId</c> quyết định ai sửa được câu
/// nào (§6). Cả hai kiểm trong <c>QuestionService</c>, vì chúng phụ thuộc dữ liệu chứ không phải
/// phụ thuộc claim.
/// </para>
/// <para>
/// Học sinh không có mặt ở bất kỳ dòng nào: các em chỉ gặp câu hỏi qua bản chụp trong đề đã sinh,
/// và bản chụp đó không mang theo <c>isCorrect</c> (§3 quy tắc ①).
/// </para>
/// </remarks>
[Authorize(Roles = SystemRoles.AdminOrTeacher)]
[Route("api/questions")]
public sealed class QuestionsController : ApiControllerBase
{
    private readonly IQuestionService _questionService;

    public QuestionsController(IQuestionService questionService)
    {
        _questionService = questionService;
    }

    /// <summary>Danh sách câu hỏi, có phân trang và lọc.</summary>
    /// <remarks>
    /// Phạm vi luôn được áp ngầm: giáo viên chỉ thấy ngân hàng môn mình, gồm câu <c>shared</c> của
    /// đồng nghiệp cùng môn cộng câu <c>private</c> của chính mình (§6).
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<QuestionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] ListQuestionsQuery query,
        CancellationToken cancellationToken)
        => FromResult(await _questionService.ListAsync(query, cancellationToken));

    /// <summary>Tập chủ đề đang có, cho dropdown và gợi ý nhập.</summary>
    /// <remarks>Khai báo trước <c>GET /{id}</c>, nếu không "topics" bị khớp vào tham số <c>{id}</c>.</remarks>
    [HttpGet("topics")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListTopics(
        [FromQuery] int? subjectId,
        CancellationToken cancellationToken)
        => FromResult(await _questionService.ListTopicsAsync(subjectId, cancellationToken));

    /// <summary>Thống kê ngân hàng câu hỏi.</summary>
    /// <remarks>
    /// Gộp ba lời gọi mà frontend đang bắn song song ở màn hình ngân hàng (§4.5). Ba request cho
    /// một màn hình là ba lần chịu độ trễ, và trên dịch vụ miễn phí có cold start thì cả ba cùng
    /// đánh thức tiến trình.
    /// </remarks>
    [HttpGet("summary")]
    [ProducesResponseType<QuestionBankSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] int? subjectId,
        CancellationToken cancellationToken)
        => FromResult(await _questionService.GetSummaryAsync(subjectId, cancellationToken));

    /// <summary>Một câu hỏi kèm đáp án.</summary>
    /// <remarks>Câu ngoài phạm vi trả <b>404</b>, không phải 403 — 403 là xác nhận id đó có thật (§4.2).</remarks>
    [HttpGet("{id:int}")]
    [ProducesResponseType<QuestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
        => FromResult(await _questionService.GetAsync(id, cancellationToken));

    /// <summary>Tạo câu hỏi. Đáp án lồng trong body, server cấp id.</summary>
    /// <remarks>
    /// Ảnh phải upload trước qua <c>POST /api/uploads/images</c> rồi gửi URL nhận được; API
    /// <b>không nhận data URI</b> (§1).
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<QuestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] SaveQuestionRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _questionService.CreateAsync(request, cancellationToken));

    /// <summary>Sửa toàn bộ câu hỏi, <b>thay cả mảng đáp án</b>.</summary>
    /// <remarks>
    /// Đáp án cũ bị xoá và id được cấp lại. Bài đã chấm giữ nguyên kết quả vì chúng chấm bằng bản
    /// chụp trong <c>ExamPaperAnswer</c>, không phải bằng bảng đáp án hiện tại (§4.5).
    /// </remarks>
    [HttpPut("{id:int}")]
    [ProducesResponseType<QuestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] SaveQuestionRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _questionService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Xoá câu hỏi.</summary>
    /// <remarks>
    /// Trả <b>409</b> khi câu đã nằm trong một đề đã sinh, kèm gợi ý ẩn câu hỏi thay vì xoá.
    /// Frontend hiện xoá thẳng không kiểm gì — đó là thứ endpoint này phải chặn lại (§4.5).
    /// </remarks>
    [HttpDelete("{id:int}")]
    [ProducesResponseType<ApiMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => FromResult(await _questionService.DeleteAsync(id, cancellationToken), "Đã xoá câu hỏi.");

    /// <summary>Bật hoặc ẩn nhanh một câu hỏi.</summary>
    /// <remarks>Ẩn chỉ loại câu khỏi các lần sinh đề <i>sau</i>; đề đã sinh dùng bản chụp nên không đổi.</remarks>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<QuestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        int id,
        [FromBody] UpdateQuestionStatusRequest request,
        CancellationToken cancellationToken)
    {
        // Validator đã bảo đảm chuỗi đọc được trước khi action chạy, nên nhánh false không với tới
        // được. Vẫn giữ để controller khỏi phải dùng dấu chấm than, cùng lý do ở StudentsController.
        if (!QuestionMappingExtensions.TryParseQuestionStatus(request.Status, out var status))
        {
            return FromFailure(Result.Failure("Trạng thái câu hỏi chỉ nhận giá trị 'active' hoặc 'hidden'."));
        }

        return FromResult(await _questionService.SetStatusAsync(id, status, cancellationToken));
    }
}
