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

    /// <summary>
    /// Danh sách câu hỏi của một đề soạn theo danh sách, theo thứ tự, kèm đáp án đúng và điểm từng câu.
    /// Rỗng với đề theo ma trận. Cùng phạm vi đọc với <see cref="GetAsync"/>.
    /// </summary>
    Task<Result<IReadOnlyList<ExamQuestionDto>>> GetQuestionsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// "Soạn tự động": rút ngẫu nhiên câu hỏi từ ngân hàng theo số câu từng mức, loại các câu đã có. Chỉ trả
    /// gợi ý, không ghi gì — đề chỉ đổi khi giáo viên lưu.
    /// </summary>
    Task<Result<ComposeQuestionsResultDto>> ComposeQuestionsAsync(
        ComposeQuestionsRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ExamDto>> CreateAsync(
        SaveExamRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Thay <b>toàn bộ</b> cấu hình của đề, theo luật của <see cref="Common.ExamEditPolicy"/>.</summary>
    /// <remarks>
    /// Chưa em nào bắt đầu làm: sửa được mọi trường, và nếu cấu hình đổi thứ bộ sinh đề đọc thì bản đề
    /// của mọi lượt giao hiện có được sinh lại trong cùng transaction — ngân hàng không đủ câu thì không
    /// ghi gì. Đã có em bắt đầu: đổi cấu hình đề trả <c>Conflict</c>; tên, mô tả và cờ hiện điểm vẫn sửa
    /// được. Lịch có luật riêng: đề đã giao thì không đổi được từ lúc còn dưới 120 phút nữa là mở cho tới
    /// hết giờ đóng (<c>Conflict</c>), gửi lại đúng lịch cũ thì không tính là đổi.
    /// </remarks>
    Task<Result<ExamDto>> UpdateAsync(
        int id,
        SaveExamRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Chỉ dời lịch mở/đóng — hẹp và an toàn hơn <see cref="UpdateAsync"/>.</summary>
    /// <remarks>Trả <c>Conflict</c> khi lịch đang khoá, như <see cref="UpdateAsync"/>.</remarks>
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
    /// <c>GET /api/exams/{id}/papers</c>. Cách đề được chia thành các đề nhỏ: số câu mỗi mức, bao nhiêu câu trùng hay dư, và đề
    /// nhỏ nào đã phát cho em nào của lớp nào. Đề không chia trả <c>paperCount = 1</c> và không có lớp nào.
    /// </summary>
    Task<Result<ExamPapersDto>> GetPapersAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuyển nháp sang xuất bản. Bổ sung so với frontend, vốn làm việc này bằng cách gửi PUT
    /// với <c>lifecycle</c> bỏ trống (§4.6).
    /// </summary>
    Task<Result<ExamDto>> PublishAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhân bản một đề của chính mình thành bản nháp mới: cùng cấu hình và tuỳ chọn, tên thêm hậu tố
    /// " (bản sao)", chưa giao cho ai. Đề của giáo viên khác trả <b>404</b> (§4.2).
    /// </summary>
    Task<Result<ExamDto>> DuplicateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mở điểm và đáp án từng câu cho học sinh: bật <c>showResultAfterSubmit</c> của đề. Chỉ làm được khi
    /// <b>hết giờ</b> — lớp đóng bài muộn nhất đã qua giờ đóng (<b>409</b> nếu còn lớp đang làm hay chưa tới giờ
    /// làm, hoặc đề chưa giao cho ai). Đã bật rồi thì trả 200 không đổi gì. Chỉ chủ đề.
    /// </summary>
    Task<Result<ExamDto>> ReleaseAnswersAsync(int id, CancellationToken cancellationToken = default);
}
