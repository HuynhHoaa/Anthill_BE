using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.ExamAttempts;
using EduQuiz.Application.Interfaces.Services;
using EduQuiz.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduQuiz.Api.Controllers;

/// <summary>
/// Năm endpoint làm bài của §4.7 — nơi tập trung nhiều quy tắc bảo mật nhất của hệ thống.
/// </summary>
/// <remarks>
/// <para>
/// Không action nào nhận <c>studentId</c>, và không action nào nhận thời gian hay điểm số từ client.
/// Danh tính suy từ JWT, đồng hồ đọc từ cột <c>ExpiresAt</c>, điểm do server tính từ bản chụp đề
/// (§3 quy tắc ②, ③, ④).
/// </para>
/// <para>
/// Ba mã trạng thái ở đây khác thói quen và đều có lý do: <b>404</b> cho lượt của người khác (403
/// là xác nhận id có thật), <b>410</b> cho cơ hội đã đóng vĩnh viễn, <b>422</b> cho dữ liệu đúng cú
/// pháp nhưng vô nghĩa như một answerId không thuộc questionId (§5).
/// </para>
/// </remarks>
[Authorize(Roles = SystemRoles.Student)]
[Route("api/exam-attempts")]
public sealed class ExamAttemptsController : ApiControllerBase
{
    private readonly IExamAttemptService _attemptService;

    public ExamAttemptsController(IExamAttemptService attemptService)
    {
        _attemptService = attemptService;
    }

    /// <summary>Bắt đầu hoặc làm tiếp một lượt. <b>Idempotent</b>.</summary>
    /// <remarks>
    /// <para>
    /// Gọi lại khi đang làm dở trả về đúng phiên cũ: cùng bộ câu, cùng thứ tự, cùng hạn giờ. Không
    /// sinh đề mới và không cấp thêm một giây nào — F5, đổi thiết bị và retry sau timeout mạng đều
    /// đi vào đây.
    /// </para>
    /// <para>
    /// Trả <b>409</b> khi đề chưa mở hoặc em đã nộp rồi (v1 không cho làm lại), <b>410</b> khi đề đã
    /// đóng hoặc lượt đã hết giờ — trường hợp sau còn khiến server chấm nốt phần đã lưu trước khi
    /// trả lời, để không còn một lượt treo lơ lửng.
    /// </para>
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<AttemptSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Start(
        [FromBody] StartAttemptRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _attemptService.StartAsync(request, cancellationToken));

    /// <summary>Nạp lại phiên đang dở sau F5 hoặc khi đổi thiết bị.</summary>
    /// <remarks>
    /// Trả về cùng hình dạng với endpoint bắt đầu, nên màn hình làm bài dựng lại chính nó mà không
    /// cần biết mình vừa vào thi hay đang quay lại. Câu hỏi kèm theo <b>không có</b> trường
    /// <c>isCorrect</c> ở bất kỳ phương án nào (§3 quy tắc ①).
    /// </remarks>
    [HttpGet("{attemptId:int}")]
    [ProducesResponseType<AttemptSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status410Gone)]
    public async Task<IActionResult> GetSession(int attemptId, CancellationToken cancellationToken)
        => FromResult(await _attemptService.GetSessionAsync(attemptId, cancellationToken));

    /// <summary>Đồng bộ đồng hồ đếm ngược với giờ server.</summary>
    /// <remarks>
    /// Bổ sung so với frontend, và cố ý nhẹ: không nạp đề, không đọc câu trả lời, không ghi gì cả.
    /// Máy học sinh chỉnh sai giờ chỉ làm lệch con số trên màn hình, còn hạn nộp thật vẫn nằm ở cột
    /// <c>ExpiresAt</c> trong database (§3 quy tắc ③).
    /// </remarks>
    [HttpGet("{attemptId:int}/time")]
    [ProducesResponseType<AttemptTimeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTime(int attemptId, CancellationToken cancellationToken)
        => FromResult(await _attemptService.GetTimeAsync(attemptId, cancellationToken));

    /// <summary>Lưu nháp. Ngữ nghĩa <b>MERGE</b> theo <c>questionId</c>, không thay cả bộ.</summary>
    /// <remarks>
    /// <para>
    /// Gói tin chỉ chứa những câu vừa đổi; câu không được nhắc tới giữ nguyên lựa chọn cũ. Kèm theo
    /// <c>clientSeq</c> tăng dần để server bỏ qua gói tới sai thứ tự — autosave chạy mỗi 800ms nên
    /// chuyện gói cũ tới sau gói mới là bình thường chứ không phải ngoại lệ (§5).
    /// </para>
    /// <para>
    /// Trả <b>410</b> khi đã hết giờ và <b>422</b> khi <c>questionId</c> không thuộc lượt này hoặc
    /// <c>answerId</c> không thuộc câu đó — không âm thầm bỏ qua, vì một câu trả lời rơi mất trong
    /// im lặng là một câu bị chấm sai mà không ai biết.
    /// </para>
    /// </remarks>
    [HttpPut("{attemptId:int}/answers")]
    [ProducesResponseType<SaveAnswersResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status410Gone)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SaveAnswers(
        int attemptId,
        [FromBody] SaveAnswersRequest request,
        CancellationToken cancellationToken)
        // FromResult(result) chu khong phai overload kem thong diep: overload do nhan Result (lop
        // cha) nen Result<SaveAnswersResponse> van khop, va client chi nhan ve mot cau chu trong
        // khi no can biet goi vua gui co duoc ghi hay khong.
        => FromResult(await _attemptService.SaveAnswersAsync(attemptId, request, cancellationToken));

    /// <summary>Nộp bài và <b>chấm ngay ở server</b>. Idempotent.</summary>
    /// <remarks>
    /// <para>
    /// Client chỉ gửi các cặp câu–đáp án; server đối chiếu bản chụp trong <c>ExamPaperAnswer</c>,
    /// đếm và tính điểm. Không bao giờ nhận <c>score</c> hay <c>correctCount</c> từ client
    /// (§3 quy tắc ②).
    /// </para>
    /// <para>
    /// Nộp lại cùng một <c>attemptId</c> trả đúng kết quả cũ với mã 200, không tạo bản ghi thứ hai.
    /// Điểm chỉ xuất hiện trong response khi giáo viên bật <c>showResultAfterSubmit</c>; không đủ
    /// điều kiện thì trường đó <b>vắng mặt</b> khỏi JSON chứ không phải bị ẩn ở giao diện (§3 ⑥).
    /// </para>
    /// </remarks>
    [HttpPost("{attemptId:int}/submit")]
    [ProducesResponseType<SubmitAttemptResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status410Gone)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(
        int attemptId,
        [FromBody] SubmitAttemptRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _attemptService.SubmitAsync(attemptId, request, cancellationToken));
}
