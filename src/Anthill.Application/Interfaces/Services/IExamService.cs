using Anthill.Application.Common;
using Anthill.Application.DTOs.Exams;

namespace Anthill.Application.Interfaces.Services;

/// <summary>Bài kiểm tra và blueprint — §4.6. Đề lưu dưới dạng cấu hình, không chứa câu hỏi cụ thể.</summary>
/// <remarks>
/// Ràng buộc đã cài (§5):
/// <list type="bullet">
/// <item><b>Làm tròn khớp từng đơn vị với frontend</b> — xem <see cref="Common.BlueprintCalculator"/>.
/// Backend <b>tự tính</b> số câu; client chỉ gửi <c>(totalQuestions, ba tỷ lệ, topic)</c>.</item>
/// <item>Bản nháp được miễn hai điều kiện nặng: tổng ba tỷ lệ bằng 100 và ngân hàng đủ câu. Đề
/// không phải nháp thì cả hai bắt buộc.</item>
/// <item><c>lifecycle</c> là dữ liệu <b>dẫn xuất</b>: chỉ <c>IsDraft</c>/<c>PublishedAt</c> nằm
/// trong DB, ba trạng thái còn lại tính theo giờ server mỗi lần đọc.</item>
/// <item><see cref="DeleteAsync"/> nằm trong transaction: xoá assignment và bản đề đã sinh trước,
/// rồi mới xoá đề; từ chối khi đã có học sinh nộp bài.</item>
/// </list>
/// </remarks>
public interface IExamService
{
    /// <summary>Danh sách đề của chính giáo viên đang đăng nhập; quản trị viên thấy toàn bộ.</summary>
    Task<Result<PagedResult<ExamDto>>> ListAsync(
        ListExamsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Chi tiết một đề. Đề của giáo viên khác trả <b>404</b>, không phải 403 (§4.2).</summary>
    Task<Result<ExamDto>> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<ExamDto>> CreateAsync(
        SaveExamRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Thay <b>toàn bộ</b> cấu hình của đề.</summary>
    Task<Result<ExamDto>> UpdateAsync(
        int id,
        SaveExamRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Chỉ dời lịch mở/đóng — hẹp và an toàn hơn <see cref="UpdateAsync"/>.</summary>
    Task<Result<ExamDto>> UpdateScheduleAsync(
        int id,
        UpdateScheduleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Xoá đề kèm mọi assignment và bản đề đã sinh. Từ chối khi đã có bài nộp.</summary>
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đối chiếu một blueprint đang gõ dở với tồn kho ngân hàng — màn hình ra đề gọi mỗi lần
    /// giáo viên kéo thanh trượt.
    /// </summary>
    Task<Result<BlueprintPreviewDto>> PreviewBlueprintAsync(
        BlueprintInput input,
        CancellationToken cancellationToken = default);

    /// <summary>Blueprint của một đề đã lưu, đối chiếu với tồn kho <b>hiện tại</b> của ngân hàng.</summary>
    Task<Result<BlueprintPreviewDto>> GetBlueprintAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuyển nháp sang xuất bản. Bổ sung so với frontend, vốn làm việc này bằng cách gửi PUT
    /// với <c>lifecycle</c> bỏ trống (§4.6).
    /// </summary>
    Task<Result<ExamDto>> PublishAsync(int id, CancellationToken cancellationToken = default);
}
