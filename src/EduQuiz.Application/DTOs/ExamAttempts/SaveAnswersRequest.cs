namespace EduQuiz.Application.DTOs.ExamAttempts;

/// <summary>Thân request của <c>PUT /api/exam-attempts/{attemptId}/answers</c> — lưu nháp.</summary>
/// <remarks>
/// <para>
/// Ngữ nghĩa <b>MERGE</b> theo <c>questionId</c>, không thay cả bộ (§4.7): gói tin chỉ chứa những
/// câu vừa đổi, những câu không nhắc tới giữ nguyên lựa chọn cũ. Hiểu nhầm thành "thay cả bộ" thì
/// mỗi lần autosave một câu là xoá sạch phần còn lại của bài.
/// </para>
/// </remarks>
public sealed record SaveAnswersRequest
{
    /// <summary>
    /// Bộ đếm tăng dần của client, dùng để bỏ qua gói tin tới sai thứ tự.
    /// </summary>
    /// <remarks>
    /// Autosave chạy mỗi 800ms; mạng chậm khiến gói cũ tới sau gói mới, và gói cũ ghi đè lên gói
    /// mới là mất đúng thao tác cuối cùng của học sinh. Server giữ <c>LastAppliedSeq</c> cho mỗi
    /// lượt và bỏ qua mọi request có số nhỏ hơn hoặc bằng.
    ///
    /// Là <b>bộ đếm</b>, không phải mốc thời gian: dùng timestamp của client nghĩa là một máy chỉnh
    /// sai giờ sẽ tự khoá mình khỏi việc lưu bài, hoặc tệ hơn, ghi đè được lên gói của chính nó (§5).
    /// </remarks>
    public required long ClientSeq { get; init; }

    public IReadOnlyList<AnswerSelectionDto> Answers { get; init; } = [];
}
