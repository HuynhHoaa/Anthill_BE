using Anthill.Application.Common;
using Anthill.Application.DTOs.Files;
using Anthill.Application.Interfaces.Services;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anthill.Api.Controllers;

/// <summary>
/// Upload ảnh cho câu hỏi và đáp án — <c>POST /api/uploads/images</c> (§4.5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Điểm chuyển đổi so với frontend</b> (§1): hiện tại frontend đọc file bằng
/// <c>FileReader.readAsDataURL</c> rồi nhét thẳng chuỗi <c>data:image/png;base64,...</c> vào
/// payload câu hỏi. Cách đó phình payload lên khoảng 4/3 kích thước ảnh, và cột <c>ImageUrl</c>
/// chỉ dài 512 ký tự nên sẽ vỡ ngay ở ảnh đầu tiên. Luồng đúng là: upload trước ở đây → nhận URL →
/// gửi URL kèm câu hỏi.
/// </para>
/// <para>
/// Chỉ giáo viên và quản trị viên gọi được. Cho học sinh upload nghĩa là mở một chỗ ghi file công
/// khai cho mọi tài khoản trong hệ thống, mà các em không có việc gì cần tới nó.
/// </para>
/// </remarks>
[Authorize(Roles = SystemRoles.AdminOrTeacher)]
[Route("api/uploads")]
public sealed class UploadsController : ApiControllerBase
{
    private readonly IFileService _fileService;

    public UploadsController(IFileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>Tải một ảnh lên và nhận về đường dẫn để gán vào câu hỏi hoặc đáp án.</summary>
    /// <remarks>
    /// Chấp nhận PNG, JPEG, GIF, WebP. Loại ảnh được xác định bằng <b>magic bytes</b> chứ không
    /// phải bằng phần mở rộng hay header <c>Content-Type</c> — cả hai thứ đó do client đặt.
    /// SVG cố ý không nằm trong danh sách: nó chứa được thẻ <c>script</c>.
    /// </remarks>
    [HttpPost("images")]
    [ProducesResponseType<UploadedFileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return FromFailure(Result.Failure("Chưa chọn file nào để tải lên."));
        }

        // Bóc IFormFile thành bốn trường thuần .NET ngay tại đây: tầng Application không được biết
        // gì về ASP.NET Core, nên kiểu này không đi xa hơn dòng dưới.
        await using var content = file.OpenReadStream();

        var request = new FileUploadRequest
        {
            Content = content,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Length = file.Length
        };

        return FromResult(await _fileService.UploadImageAsync(request, cancellationToken));
    }
}
