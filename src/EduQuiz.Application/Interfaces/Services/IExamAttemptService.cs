using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.ExamAttempts;

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
/// PUT  /api/exam-attempts/{attemptId}/answers → Task&lt;Result&lt;SaveAnswersResponse&gt;&gt; SaveAnswersAsync(int attemptId, SaveAnswersRequest request, CancellationToken ct)
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
    /// <summary>Bài được giao cho chính học sinh đang đăng nhập.</summary>
    Task<Result<IReadOnlyList<StudentExamItemDto>>> ListMyExamsAsync(
        ListMyExamsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Chi tiết một mục, cho màn giới thiệu trước khi vào thi.</summary>
    /// <remarks>
    /// Assignment của bạn cùng lớp trả <see cref="ErrorCode.NotFound"/> chứ không phải Forbidden —
    /// trả 403 là xác nhận id đó có thật, đủ để dò dần danh sách bài của cả trường (§4.2).
    /// </remarks>
    Task<Result<StudentExamItemDto>> GetMyExamAsync(int assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Bắt đầu <b>hoặc làm tiếp</b> một lượt. Idempotent.</summary>
    /// <remarks>
    /// Gọi lại khi đang làm dở trả về đúng phiên cũ: không sinh đề mới, không tạo lượt thứ hai và
    /// không cấp thêm giờ. F5, đổi thiết bị và retry sau timeout mạng đều đi qua đây.
    /// </remarks>
    Task<Result<AttemptSessionDto>> StartAsync(
        StartAttemptRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Nạp lại phiên đang dở sau F5 hoặc đổi thiết bị.</summary>
    Task<Result<AttemptSessionDto>> GetSessionAsync(int attemptId, CancellationToken cancellationToken = default);

    /// <summary>Đồng bộ đồng hồ với giờ server. Không nạp đề, không có tác dụng phụ.</summary>
    Task<Result<AttemptTimeDto>> GetTimeAsync(int attemptId, CancellationToken cancellationToken = default);

    /// <summary>Lưu nháp, ngữ nghĩa MERGE theo <c>questionId</c>.</summary>
    /// <remarks>
    /// Trả <see cref="ErrorCode.Gone"/> (410) khi đã quá <c>ExpiresAt</c>, và
    /// <see cref="ErrorCode.Unprocessable"/> (422) khi <c>questionId</c> không thuộc lượt này hoặc
    /// <c>answerId</c> không thuộc <c>questionId</c> đó.
    /// </remarks>
    Task<Result<SaveAnswersResponse>> SaveAnswersAsync(
        int attemptId,
        SaveAnswersRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Nộp bài và chấm ngay ở server. Idempotent.</summary>
    Task<Result<SubmitAttemptResponse>> SubmitAsync(
        int attemptId,
        SubmitAttemptRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chấm nốt những lượt đã quá hạn mà chưa ai nộp. Trả về số lượt vừa xử lý.
    /// </summary>
    /// <remarks>
    /// Do <c>BackgroundService</c> gọi chứ không phải client bấm giờ: học sinh tắt trình duyệt thì
    /// không ai gọi submit cả (§3 quy tắc ③, §7). Không đọc <c>ICurrentUser</c> vì chạy ngoài mọi
    /// request HTTP.
    /// </remarks>
    Task<Result<int>> AutoSubmitExpiredAsync(int batchSize, CancellationToken cancellationToken = default);
}
