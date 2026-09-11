using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.ExamAttempts;
using EduQuiz.Application.Interfaces.Services;
using EduQuiz.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduQuiz.Api.Controllers;

/// <summary>
/// Hai endpoint đầu của §4.7 — danh sách bài được giao cho chính học sinh đang đăng nhập.
/// </summary>
/// <remarks>
/// <para>
/// Đường dẫn bắt đầu bằng <c>/api/my</c> chứ không phải <c>/api/students/{id}/exams</c>, và đó là
/// một quyết định bảo mật chứ không phải chuyện đặt tên: một đường dẫn không mang id thì không có
/// gì để sửa trên thanh địa chỉ. Chủ sở hữu suy từ JWT ở tầng service (§3 quy tắc ④).
/// </para>
/// <para>
/// Chỉ vai trò <c>Student</c> vào được. Giáo viên xem tiến trình của cả lớp qua
/// <c>GET /api/exams/{id}/assignments</c>, nơi dữ liệu trả về được cắt theo góc nhìn của người dạy.
/// </para>
/// </remarks>
[Authorize(Roles = SystemRoles.Student)]
[Route("api/my/exams")]
public sealed class MyExamsController : ApiControllerBase
{
    private readonly IExamAttemptService _attemptService;

    public MyExamsController(IExamAttemptService attemptService)
    {
        _attemptService = attemptService;
    }

    /// <summary>Bài kiểm tra được giao cho chính mình, mới giao trước.</summary>
    /// <remarks>
    /// Không phân trang: một học sinh có nhiều nhất vài chục bài trong một học kỳ. Trạng thái từng
    /// dòng là dữ liệu dẫn xuất — chưa có lượt làm bài thì <c>assigned</c>, khung giờ của em đã đóng
    /// (lịch riêng của lớp nếu có) mà chưa từng bắt đầu thì <c>expired</c>.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<StudentExamItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] ListMyExamsQuery query,
        CancellationToken cancellationToken)
        => FromResult(await _attemptService.ListMyExamsAsync(query, cancellationToken));

    /// <summary>Chi tiết một mục, cho màn giới thiệu trước khi vào thi.</summary>
    /// <remarks>
    /// Trả <b>404</b> khi bản ghi thuộc về học sinh khác — không phải 403. Trả 403 là xác nhận id
    /// đó có thật, đủ để dò dần danh sách bài của cả trường bằng một vòng lặp (§4.2).
    /// </remarks>
    [HttpGet("{assignmentId:int}")]
    [ProducesResponseType<StudentExamItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int assignmentId, CancellationToken cancellationToken)
        => FromResult(await _attemptService.GetMyExamAsync(assignmentId, cancellationToken));
}
