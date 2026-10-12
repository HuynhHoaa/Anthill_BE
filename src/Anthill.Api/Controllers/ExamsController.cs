using Anthill.Application.Common;
using Anthill.Application.DTOs.Exams;
using Anthill.Application.Interfaces.Services;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anthill.Api.Controllers;

/// <summary>
/// Mười endpoint quản lý bài kiểm tra và blueprint của §4.6.
/// </summary>
/// <remarks>
/// <para>
/// Vai trò chia hai mức ngay ở tầng này: <b>đọc</b> mở cho cả giáo viên lẫn quản trị viên, còn
/// <b>ghi</b> chỉ giáo viên. Quản trị viên không thuộc môn nào nên không có ngân hàng câu hỏi để
/// rút — cho họ tạo đề là tạo ra một đề không bao giờ sinh nổi câu hỏi.
/// </para>
/// <para>
/// Học sinh không xuất hiện ở bất kỳ dòng nào. Các em nhìn thấy đề qua <c>/api/my/exams</c>, nơi
/// chỉ trả về những gì đã được giao cho chính mình và không kèm cấu hình blueprint (§3 quy tắc ④).
/// </para>
/// </remarks>
[Authorize(Roles = SystemRoles.AdminOrTeacher)]
[Route("api/exams")]
public sealed class ExamsController : ApiControllerBase
{
    private readonly IExamService _examService;

    public ExamsController(IExamService examService)
    {
        _examService = examService;
    }

    /// <summary>Danh sách đề, có phân trang, tìm kiếm và lọc theo vòng đời.</summary>
    /// <remarks>Giáo viên chỉ thấy đề mình tạo; quản trị viên thấy toàn bộ.</remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<ExamDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] ListExamsQuery query,
        CancellationToken cancellationToken)
        => FromResult(await _examService.ListAsync(query, cancellationToken));

    /// <summary>Đối chiếu một blueprint đang gõ dở với tồn kho ngân hàng câu hỏi.</summary>
    /// <remarks>
    /// <para>
    /// Trái tim của tính năng: màn hình ra đề gọi endpoint này mỗi lần giáo viên kéo thanh trượt
    /// tỷ lệ, để biết đề có sinh nổi hay không <i>trước</i> khi bấm lưu.
    /// </para>
    /// <para>
    /// Client chỉ gửi tỷ lệ; <b>số câu từng mức do server tính</b> (§5). Phép làm tròn khớp từng
    /// đơn vị với frontend, nếu không thì bản xem trước và đề sinh ra lệch nhau một câu.
    /// </para>
    /// </remarks>
    [HttpPost("blueprint-preview")]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<BlueprintPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PreviewBlueprint(
        [FromBody] BlueprintInput input,
        CancellationToken cancellationToken)
        => FromResult(await _examService.PreviewBlueprintAsync(input, cancellationToken));

    /// <summary>Chi tiết một đề.</summary>
    /// <remarks>Đề của giáo viên khác trả <b>404</b>, không phải 403 — 403 là xác nhận id đó có thật (§4.2).</remarks>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ExamDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
        => FromResult(await _examService.GetAsync(id, cancellationToken));

    /// <summary>Blueprint của một đề đã lưu, đối chiếu với tồn kho <b>hiện tại</b>.</summary>
    /// <remarks>
    /// Một đề hợp lệ tuần trước có thể không sinh nổi hôm nay vì đồng nghiệp vừa ẩn bớt câu hỏi —
    /// đó chính là điều giáo viên cần biết trước khi bấm giao bài.
    /// </remarks>
    [HttpGet("{id:int}/blueprint")]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<BlueprintPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlueprint(int id, CancellationToken cancellationToken)
        => FromResult(await _examService.GetBlueprintAsync(id, cancellationToken));

    /// <summary>Tạo đề theo blueprint. Đề <b>không</b> chứa danh sách câu hỏi cụ thể.</summary>
    /// <remarks>
    /// Gửi <c>lifecycle: "draft"</c> để lưu bản nháp — bản nháp được miễn hai điều kiện nặng nhất
    /// (tổng ba tỷ lệ bằng 100 và ngân hàng đủ câu). Bỏ trống trường đó nghĩa là đề có hiệu lực
    /// theo lịch, và khi ấy cả hai điều kiện đều bắt buộc.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<ExamDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] SaveExamRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _examService.CreateAsync(request, cancellationToken));

    /// <summary>Thay <b>toàn bộ</b> cấu hình của đề.</summary>
    /// <remarks>
    /// <para>Chỉ muốn dời lịch thì dùng <c>PATCH /{id}/schedule</c> — hẹp hơn nên an toàn hơn.</para>
    /// <para>
    /// Chưa em nào bắt đầu làm thì sửa được mọi thứ, và bản đề đã sinh cho các lượt giao được sinh lại
    /// theo cấu hình mới. Đã có em bắt đầu thì đổi cấu hình đề (số câu, tỷ lệ, chủ đề, điểm, thời lượng,
    /// tuỳ chọn trộn) hay đưa đề về nháp trả <b>409</b>; tên, mô tả và cờ hiện điểm vẫn lưu được.
    /// </para>
    /// <para>
    /// Đề đã giao thì đổi lịch cũng trả <b>409</b> từ lúc còn dưới 120 phút nữa là mở cho tới hết giờ đóng.
    /// Gửi lại đúng lịch đang có thì không bị chặn.
    /// </para>
    /// </remarks>
    [HttpPut("{id:int}")]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<ExamDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] SaveExamRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _examService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Chỉ dời lịch mở và đóng.</summary>
    /// <remarks>
    /// Tồn tại riêng vì đây là thao tác hay làm nhất sau khi đề đã giao. Dùng PUT cho việc này
    /// nghĩa là gửi lại trọn cấu hình, và chỉ cần client bỏ sót một trường là tỷ lệ độ khó hoặc
    /// điểm từng mức bị ghi đè bằng giá trị mặc định — trên một đề học sinh đang chờ làm.
    /// Trả <b>409</b> khi đề đã giao và đang trong lúc khoá giờ (từ dưới 120 phút trước giờ mở tới hết giờ đóng).
    /// </remarks>
    [HttpPatch("{id:int}/schedule")]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<ExamDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSchedule(
        int id,
        [FromBody] UpdateScheduleRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _examService.UpdateScheduleAsync(id, request, cancellationToken));

    /// <summary>Chuyển bản nháp sang xuất bản.</summary>
    /// <remarks>
    /// Bổ sung so với frontend, vốn làm việc này bằng cách gửi PUT với <c>lifecycle</c> bỏ trống —
    /// tức là gửi lại trọn cấu hình chỉ để lật một cờ. Trả <b>409</b> khi đề đã xuất bản rồi.
    /// </remarks>
    [HttpPost("{id:int}/publish")]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<ExamDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(int id, CancellationToken cancellationToken)
        => FromResult(await _examService.PublishAsync(id, cancellationToken));

    /// <summary>Nhân bản đề thành một bản nháp mới, cùng cấu hình, chưa giao cho ai.</summary>
    /// <remarks>
    /// Lối ra khi cấu hình đề đã bị khoá vì có học sinh bắt đầu làm: muốn đổi số câu hay tỷ lệ thì sửa
    /// trên bản sao rồi giao lại. Chỉ nhân bản được đề của chính mình; đề của giáo viên khác trả
    /// <b>404</b> (§4.2).
    /// </remarks>
    [HttpPost("{id:int}/duplicate")]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<ExamDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Duplicate(int id, CancellationToken cancellationToken)
        => FromResult(await _examService.DuplicateAsync(id, cancellationToken));

    /// <summary>Xoá đề kèm mọi bản ghi giao bài và bản đề đã sinh.</summary>
    /// <remarks>
    /// Trả <b>409</b> khi đã có học sinh nộp bài: xoá lúc đó là xoá luôn kết quả của các em.
    /// Cả ba lệnh xoá nằm trong một transaction.
    /// </remarks>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = SystemRoles.Teacher)]
    [ProducesResponseType<ApiMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => FromResult(await _examService.DeleteAsync(id, cancellationToken), "Đã xoá bài kiểm tra.");
}
