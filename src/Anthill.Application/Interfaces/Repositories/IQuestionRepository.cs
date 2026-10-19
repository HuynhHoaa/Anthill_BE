using Anthill.Application.Common;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Interfaces.Repositories;

/// <summary>
/// Ngân hàng câu hỏi dùng chung theo tổ bộ môn. Mọi truy vấn đọc đều phải kèm phạm vi
/// của §6: <c>SubjectId = currentSubjectId AND (Visibility = Shared OR CreatedByUserId = currentUserId)</c>.
/// Quyền <b>ghi</b> kiểm riêng ở tầng service, không phải ở đây.
/// </summary>
/// <remarks>
/// <b>Vì sao <c>subjectId</c> nullable ở ba phương thức đọc.</b> Mệnh đề phạm vi của §6 được viết
/// cho giáo viên, người luôn thuộc đúng một môn. Quản trị viên thì không thuộc môn nào —
/// <c>User.SubjectId</c> của họ là NULL — nên không có giá trị nào để so. NULL ở đây nghĩa là
/// "mọi môn", cùng quy ước với <c>IStudentGroupRepository.GetOwnedAsync</c>.
/// <para>
/// Quản trị viên thu hẹp về một môn thì <c>subjectId</c> khác NULL, nên ba phương thức đó nhận thêm
/// <c>includePrivateOfOthers</c>: bật lên thì bỏ mệnh đề <c>Visibility</c>, để lọc theo môn không làm
/// mất những câu riêng tư mà ngân hàng không lọc vẫn hiện.
/// </para>
/// <para>
/// <see cref="GetBlueprintCandidatesAsync"/> cố ý <b>không</b> nullable: sinh đề luôn diễn ra
/// trong đúng một môn của người tạo đề, và một đề rút câu từ mọi môn là vô nghĩa.
/// </para>
/// </remarks>
public interface IQuestionRepository : IGenericRepository<Question>
{
    /// <summary><paramref name="subjectId"/> NULL nghĩa là mọi môn — chỉ quản trị viên nhận giá trị đó.</summary>
    Task<PagedResult<Question>> GetPagedAsync(
        int? subjectId,
        int currentUserId,
        string? search,
        string? topic,
        QuestionDifficulty? difficulty,
        QuestionStatus? status,
        PaginationParams pagination,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<int>? excludeQuestionIds = null,
        bool includePrivateOfOthers = false);

    Task<Question?> GetWithAnswersAsync(int questionId, CancellationToken cancellationToken = default);

    /// <summary>Tập chủ đề đang có trong phạm vi người gọi, cho dropdown và gợi ý nhập.</summary>
    Task<IReadOnlyList<string>> GetTopicsAsync(
        int? subjectId,
        int currentUserId,
        CancellationToken cancellationToken = default,
        bool includePrivateOfOthers = false);

    /// <summary>
    /// Số câu theo từng mức độ. Gộp ba lời gọi mà frontend đang bắn song song ở màn hình
    /// ngân hàng câu hỏi và ở bước xem trước blueprint.
    /// </summary>
    Task<IReadOnlyDictionary<QuestionDifficulty, int>> CountByDifficultyAsync(
        int? subjectId,
        int currentUserId,
        string? topic = null,
        QuestionStatus? status = null,
        CancellationToken cancellationToken = default,
        bool includePrivateOfOthers = false);

    /// <summary>
    /// Toàn bộ câu đủ điều kiện đưa vào đề ở một mức độ, kèm đáp án. Việc bốc ngẫu nhiên
    /// làm ở tầng service bằng Fisher-Yates <b>có seed</b> — không dùng ORDER BY RANDOM(),
    /// vì §5 đòi cùng một seed phải cho ra cùng một bộ câu sau khi học sinh F5.
    /// </summary>
    Task<IReadOnlyList<Question>> GetBlueprintCandidatesAsync(
        int subjectId,
        int currentUserId,
        string? topic,
        QuestionDifficulty difficulty,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Các câu có id trong <paramref name="ids"/> mà người gọi đọc được (cùng phạm vi đọc §6), kèm đáp án và
    /// người soạn. Câu nào ngoài phạm vi, hoặc không tồn tại, đơn giản là vắng mặt trong kết quả — nên
    /// người gọi so số lượng để biết có câu nào không dùng được.
    /// </summary>
    Task<IReadOnlyList<Question>> GetInScopeWithAnswersByIdsAsync(
        int subjectId,
        int currentUserId,
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Câu hỏi đã từng được đưa vào một đề đã sinh, hoặc đang nằm trong danh sách câu của một đề. §4.5 bắt
    /// buộc kiểm trước khi xoá — frontend hiện xoá thẳng, không kiểm gì.
    /// </summary>
    Task<bool> IsUsedInAnyPaperAsync(int questionId, CancellationToken cancellationToken = default);

    Task<int> CountInSubjectAsync(int subjectId, CancellationToken cancellationToken = default);
}
