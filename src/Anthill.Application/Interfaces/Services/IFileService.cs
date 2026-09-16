using Anthill.Application.Common;
using Anthill.Application.DTOs.Files;

namespace Anthill.Application.Interfaces.Services;

/// <summary>Upload ảnh cho câu hỏi và đáp án — <c>POST /api/uploads/images</c> (§4.5).</summary>
/// <remarks>
/// Luồng ảnh (§1): frontend upload ảnh trước qua service này, nhận về URL, rồi mới gửi URL đó
/// kèm câu hỏi. API <b>không nhận data URI</b> trong payload câu hỏi.
/// <see cref="FileUploadRequest"/> nhận luồng byte và tên file gốc, không nhận <c>IFormFile</c>:
/// tầng Application không được biết ASP.NET Core.
/// Ràng buộc: chặn theo phần mở rộng <b>và</b> magic bytes, giới hạn dung lượng,
/// đặt tên file ngẫu nhiên chứ không dùng tên người dùng gửi lên.
/// </remarks>
public interface IFileService
{
    /// <summary>
    /// Lưu một ảnh và trả về đường dẫn tương đối để gán vào <c>Question.ImageUrl</c> hoặc
    /// <c>AnswerOption.ImageUrl</c>.
    /// </summary>
    Task<Result<UploadedFileDto>> UploadImageAsync(
        FileUploadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xoá một file theo đúng đường dẫn tương đối đã cấp.
    /// </summary>
    /// <remarks>
    /// Trả <c>Success</c> khi file vốn đã không còn: mục đích là "bảo đảm nó không tồn tại", mà
    /// điều đó đã đúng sẵn. Đường dẫn nằm ngoài thư mục upload luôn bị từ chối — tham số này đến
    /// từ dữ liệu người dùng nên là đường đi kinh điển của lỗi vượt thư mục.
    /// </remarks>
    Task<Result> DeleteAsync(string? relativeUrl, CancellationToken cancellationToken = default);
}
