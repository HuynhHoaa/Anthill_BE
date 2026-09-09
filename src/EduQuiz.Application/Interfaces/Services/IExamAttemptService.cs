namespace EduQuiz.Application.Interfaces.Services;

/// <summary>
/// Làm bài phía học sinh — §4.7. Không endpoint nào ở đây nhận <c>studentId</c>:
/// danh tính suy từ JWT qua <c>ICurrentUser</c> (§3 quy tắc ④).
/// </summary>
/// <remarks>
/// <b>Bản đồ đường đi</b> (chờ <c>DTOs/ExamAttempts/</c>):
/// <code>
/// GET  /api/my/exams                          → Task&lt;Result&lt;IReadOnlyList&lt;StudentExamItemDto&gt;&gt;&gt; ListMyExamsAsync(ListMyExamsQuery query, CancellationToken ct)
/// GET  /api/my/exams/{assignmentId}           → Task&lt;Result&lt;StudentExamItemDto&gt;&gt; GetMyExamAsync(int assignmentId, CancellationToken ct)
/// POST /api/exam-attempts                     → Task&lt;Result&lt;AttemptSessionDto&gt;&gt; StartAsync(StartAttemptRequest request, CancellationToken ct)
/// GET  /api/exam-attempts/{attemptId}         → Task&lt;Result&lt;AttemptSessionDto&gt;&gt; GetSessionAsync(int attemptId, CancellationToken ct)
/// GET  /api/exam-attempts/{attemptId}/time    → Task&lt;Result&lt;AttemptTimeDto&gt;&gt; GetTimeAsync(int attemptId, CancellationToken ct)
/// PUT  /api/exam-attempts/{attemptId}/answers → Task&lt;Result&gt; SaveAnswersAsync(int attemptId, SaveAnswersRequest request, CancellationToken ct)
/// POST /api/exam-attempts/{attemptId}/submit  → Task&lt;Result&lt;SubmitAttemptResponse&gt;&gt; SubmitAsync(int attemptId, SubmitAttemptRequest request, CancellationToken ct)
/// (background job)                            → Task&lt;Result&lt;int&gt;&gt; AutoSubmitExpiredAsync(int batchSize, CancellationToken ct)
/// </code>
/// Đây là nơi tập trung nhiều quy tắc bảo mật nhất, phải cài đủ:
/// <list type="bullet">
/// <item><b>Đáp án đúng không rời server trong lúc làm bài (§3 ①).</b> Hai DTO tách biệt:
/// <c>AttemptAnswerOptionDto { id, content, imageUrl }</c> lúc làm bài — <b>không khai báo</b>
/// trường <c>isCorrect</c>, kể cả gán false — và
/// <c>ResultAnswerOptionDto { id, content, imageUrl, isCorrect }</c> lúc xem kết quả.
/// Học sinh mở DevTools là thấy toàn bộ response.</item>
/// <item><b>Chấm điểm ở server, luôn luôn (§3 ②).</b> Client chỉ gửi cặp questionId-answerId;
/// server đối chiếu <c>ExamPaperAnswer.IsCorrect</c> trong snapshot, đếm và tính điểm.
/// Không bao giờ nhận <c>score</c> hay <c>correctCount</c> từ client.</item>
/// <item><b>Hạn giờ theo đồng hồ server (§3 ③).</b> <c>ExpiresAt = StartedAt + DurationMinutes</c>
/// chốt ngay lúc bắt đầu; mọi kiểm tra so với <c>DateTime.UtcNow</c>.</item>
/// <item><b>Bắt đầu là idempotent:</b> gọi lại trên assignment đã có attempt thì trả về đúng
/// phiên cũ, không sinh đề mới.</item>
/// <item><b>Chống ghi đè ngược thứ tự:</b> autosave chạy mỗi 800ms, mạng chậm khiến gói cũ tới
/// sau gói mới. Giữ <c>LastAppliedSeq</c> cho mỗi attempt và bỏ qua request có <c>clientSeq</c>
/// nhỏ hơn. Không dùng timestamp của client làm mốc.</item>
/// <item><b>Lưu nháp mang ngữ nghĩa MERGE</b> theo questionId, không thay cả bộ.</item>
/// <item><c>SaveAnswersAsync</c> trả <see cref="Common.ErrorCode.Gone"/> (410) khi
/// serverNow đã vượt <c>ExpiresAt</c>.</item>
/// <item><b>Validate đáp án gửi lên:</b> questionId phải thuộc đúng attempt đó, answerId phải
/// thuộc đúng questionId đó. Trả <see cref="Common.ErrorCode.Unprocessable"/> (422),
/// không âm thầm bỏ qua.</item>
/// <item><b>Nộp idempotent:</b> nộp lại cùng attemptId trả đúng kết quả cũ, không tạo bản ghi
/// thứ hai. Cờ chống bấm hai lần ở frontend không đủ — retry sau timeout mạng vẫn tới server
/// hai lần, chỉ khoá ở database mới chắc.</item>
/// <item><c>AutoSubmitExpiredAsync</c> do <c>BackgroundService</c> gọi chứ không phải client bấm
/// giờ: học sinh tắt trình duyệt thì không ai gọi submit cả (§3 ③, §7).</item>
/// </list>
/// </remarks>
public interface IExamAttemptService
{
}
