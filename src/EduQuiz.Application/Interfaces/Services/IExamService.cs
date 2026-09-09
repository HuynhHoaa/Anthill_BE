namespace EduQuiz.Application.Interfaces.Services;

/// <summary>Bài kiểm tra và blueprint — §4.6. Đề lưu dưới dạng cấu hình, không chứa câu hỏi cụ thể.</summary>
/// <remarks>
/// <b>Bản đồ đường đi</b> (chờ <c>DTOs/Exams/</c>):
/// <code>
/// GET    /api/exams                    → Task&lt;Result&lt;PagedResult&lt;ExamDto&gt;&gt;&gt; ListAsync(ListExamsQuery query, CancellationToken ct)
/// GET    /api/exams/{id}               → Task&lt;Result&lt;ExamDto&gt;&gt; GetAsync(int id, CancellationToken ct)
/// POST   /api/exams                    → Task&lt;Result&lt;ExamDto&gt;&gt; CreateAsync(SaveExamRequest request, CancellationToken ct)
/// PUT    /api/exams/{id}               → Task&lt;Result&lt;ExamDto&gt;&gt; UpdateAsync(int id, SaveExamRequest request, CancellationToken ct)
/// PATCH  /api/exams/{id}/schedule      → Task&lt;Result&lt;ExamDto&gt;&gt; UpdateScheduleAsync(int id, UpdateScheduleRequest request, CancellationToken ct)
/// DELETE /api/exams/{id}               → Task&lt;Result&gt; DeleteAsync(int id, CancellationToken ct)
/// POST   /api/exams/blueprint-preview  → Task&lt;Result&lt;BlueprintPreviewDto&gt;&gt; PreviewBlueprintAsync(BlueprintInput input, CancellationToken ct)
/// GET    /api/exams/{id}/blueprint     → Task&lt;Result&lt;BlueprintPreviewDto&gt;&gt; GetBlueprintAsync(int id, CancellationToken ct)
/// POST   /api/exams/{id}/publish       → Task&lt;Result&lt;ExamDto&gt;&gt; PublishAsync(int id, CancellationToken ct)
/// </code>
/// Ràng buộc phải cài (§5):
/// <list type="bullet">
/// <item><b>Làm tròn khớp từng đơn vị với frontend.</b>
/// <c>easy = clamp(round(total * e / 100), 0, total)</c> ·
/// <c>medium = min(total - easy, max(0, round(total * m / 100)))</c> ·
/// <c>hard = total - easy - medium</c> (mức Khó nhận phần dư nên tổng luôn bằng <c>totalQuestions</c>).
/// Trong .NET phải dùng <c>MidpointRounding.AwayFromZero</c> để khớp <c>Math.round</c> của
/// JavaScript, nếu không lệch một câu ở các mốc <c>.5</c>.</item>
/// <item>Backend <b>tự tính</b> số câu; client chỉ gửi <c>(totalQuestions, ba tỷ lệ, topic)</c>.</item>
/// <item>Validate: <c>title</c> không rỗng sau trim · <c>1 ≤ durationMinutes ≤ 300</c> ·
/// <c>totalQuestions ≥ 1</c> · <c>pointPerQuestion &gt; 0</c> · <c>closeAt &gt; openAt</c> ·
/// <c>durationMinutes</c> không lớn hơn khoảng cách <c>openAt..closeAt</c> (frontend mới chỉ
/// cảnh báo ở giao diện) · nếu không phải bản nháp thì tổng ba tỷ lệ phải bằng 100 và ngân hàng
/// phải đủ câu.</item>
/// <item><c>lifecycle</c> là dữ liệu <b>dẫn xuất</b>, không lưu cứng: chỉ <c>IsDraft</c>/<c>PublishedAt</c>
/// nằm trong DB, ba trạng thái còn lại tính theo giờ server mỗi lần đọc.</item>
/// <item><c>DeleteAsync</c> nằm trong transaction: xoá assignment và bản đề đã sinh trước, rồi mới
/// xoá đề; từ chối khi đã có học sinh nộp bài.</item>
/// <item>Sửa đề đã giao phải bắn <c>ExamUpdated</c> cho học sinh liên quan (§7).</item>
/// </list>
/// </remarks>
public interface IExamService
{
}
