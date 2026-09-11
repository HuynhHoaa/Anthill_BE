using Anthill.Application.Common;
using Anthill.Application.DTOs.Questions;
using Anthill.Domain.Enums;

namespace Anthill.Application.Interfaces.Services;

/// <summary>Ngân hàng câu hỏi — §4.5. Dùng chung theo tổ bộ môn, phạm vi chặn bằng <c>SubjectId</c>.</summary>
/// <remarks>
/// Ràng buộc phải cài: phạm vi đọc là
/// <c>SubjectId = currentSubjectId AND (Visibility = Shared OR CreatedByUserId = currentUserId)</c> (§6) ·
/// quyền ghi là <c>CreatedByUserId == currentUserId || Role == Admin</c> ·
/// đúng <b>một</b> đáp án đúng và ít nhất hai đáp án · điểm mặc định &gt; 0 ·
/// sửa câu hỏi <b>thay cả mảng đáp án</b> chứ không sửa tại chỗ, để bài đã chấm giữ nguyên đáp án cũ (§4.5) ·
/// <see cref="DeleteAsync"/> phải từ chối khi câu hỏi đã nằm trong một đề đã sinh
/// (<c>IQuestionRepository.IsUsedInAnyPaperAsync</c>) — frontend hiện xoá thẳng, không kiểm gì ·
/// <see cref="GetSummaryAsync"/> gộp ba lời gọi mà frontend đang bắn song song.
/// <para>
/// <b>Hai vai trò, hai phạm vi.</b> Giáo viên bị giới hạn trong môn của mình, đọc được câu
/// <c>Shared</c> của đồng nghiệp cùng môn cộng câu <c>Private</c> của chính mình. Quản trị viên
/// không thuộc môn nào nên đọc toàn bộ; §6 cũng cho họ quyền ghi trên mọi câu.
/// </para>
/// </remarks>
public interface IQuestionService
{
    /// <summary>Danh sách có phân trang, lọc theo từ khoá / chủ đề / độ khó / trạng thái.</summary>
    Task<Result<PagedResult<QuestionDto>>> ListAsync(
        ListQuestionsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Tập chủ đề đang có trong phạm vi người gọi, cho dropdown và gợi ý nhập.</summary>
    Task<Result<IReadOnlyList<string>>> ListTopicsAsync(
        int? subjectId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Thống kê ngân hàng. Gộp ba lời gọi mà frontend đang bắn song song (§4.5).</summary>
    Task<Result<QuestionBankSummaryDto>> GetSummaryAsync(
        int? subjectId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Một câu hỏi kèm đáp án. Câu ngoài phạm vi người gọi trả <c>NotFound</c>, không phải
    /// <c>Forbidden</c> — trả 403 là xác nhận id đó có thật (§4.2).
    /// </summary>
    Task<Result<QuestionDto>> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Tạo câu hỏi. Đáp án lồng trong body, server cấp id.</summary>
    Task<Result<QuestionDto>> CreateAsync(
        SaveQuestionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sửa toàn bộ, <b>thay cả mảng đáp án</b>. Bài đã chấm không bị ảnh hưởng vì chúng chấm bằng
    /// bản chụp trong <c>ExamPaperAnswer</c>, không phải bằng bảng <c>AnswerOptions</c> (§4.5).
    /// </summary>
    Task<Result<QuestionDto>> UpdateAsync(
        int id,
        SaveQuestionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xoá câu hỏi. Trả <c>Conflict</c> khi câu đã nằm trong một đề đã sinh — xoá lúc đó sẽ kéo
    /// theo dữ liệu của những bài đã làm.
    /// </summary>
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Bật hoặc ẩn nhanh. Ẩn chỉ loại câu khỏi lần sinh đề <i>sau</i>, không đụng đề đã sinh.</summary>
    Task<Result<QuestionDto>> SetStatusAsync(
        int id,
        QuestionStatus status,
        CancellationToken cancellationToken = default);
}
