namespace Anthill.Application.DTOs.ExamAttempts;

/// <summary>Thân request của <c>POST /api/exam-attempts/{attemptId}/submit</c>.</summary>
/// <remarks>
/// <para>
/// Mang theo <see cref="Answers"/> để lần bấm cuối cùng không rơi vào khe giữa hai nhịp autosave:
/// học sinh tích câu cuối rồi bấm Nộp ngay có thể nộp trước khi gói lưu nháp kịp đi.
/// </para>
/// <para>
/// Tuyệt đối <b>không</b> có <c>score</c> hay <c>correctCount</c>. Frontend hiện tự tính điểm rồi
/// ghi vào mock; nhận hai trường đó từ client nghĩa là ai cũng tự cho mình mười điểm (§3 quy tắc ②).
/// </para>
/// </remarks>
public sealed record SubmitAttemptRequest
{
    /// <summary>
    /// Bộ đếm của gói cuối, cùng ý nghĩa với <c>SaveAnswersRequest.ClientSeq</c>. Bỏ trống khi
    /// client không có gì mới để gửi kèm.
    /// </summary>
    public long? ClientSeq { get; init; }

    /// <summary>Những câu vừa đổi, cùng ngữ nghĩa MERGE với lưu nháp. Rỗng là hợp lệ.</summary>
    public IReadOnlyList<AnswerSelectionDto> Answers { get; init; } = [];
}
