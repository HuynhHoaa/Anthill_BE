using Anthill.Domain.Entities;

namespace Anthill.Application.Interfaces.Repositories;

/// <summary>
/// Lựa chọn của học sinh, duy nhất theo cặp <c>(AttemptId, ExamPaperQuestionId)</c>; câu nhiều đáp án đúng
/// giữ cả tập phương án đã chọn trong một dòng.
/// Lưu nháp mang ngữ nghĩa <b>MERGE</b> theo câu hỏi, không thay cả bộ (§4.7) — nên
/// repository cần đọc theo tập câu hỏi để cập nhật đúng những câu client vừa gửi.
/// </summary>
public interface IStudentAnswerRepository : IGenericRepository<StudentAnswer>
{
    Task<IReadOnlyList<StudentAnswer>> GetByAttemptAsync(int attemptId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentAnswer>> GetByAttemptAndQuestionsAsync(
        int attemptId,
        IEnumerable<int> examPaperQuestionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Map <c>ExamPaperQuestionId -&gt; id các phương án đã chọn</c> để dựng <c>savedAnswers</c> của phiên làm bài.
    /// Câu bỏ trống có tập rỗng.
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetSelectionMapAsync(
        int attemptId,
        CancellationToken cancellationToken = default);

    Task RemoveByAttemptAsync(int attemptId, CancellationToken cancellationToken = default);
}
