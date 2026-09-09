namespace EduQuiz.Application.Interfaces.Services;

/// <summary>Giao bài và thu hồi — §4.6 phần assignment.</summary>
/// <remarks>
/// <b>Bản đồ đường đi</b> (chờ <c>DTOs/ExamAssignments/</c>):
/// <code>
/// GET    /api/exams/{id}/assignments                    → Task&lt;Result&lt;IReadOnlyList&lt;ExamAssignmentDto&gt;&gt;&gt; ListAsync(int examId, CancellationToken ct)
/// POST   /api/exams/{id}/assignments                    → Task&lt;Result&lt;AssignExamResponse&gt;&gt; AssignAsync(int examId, AssignExamRequest request, CancellationToken ct)
/// DELETE /api/exams/{examId}/assignments/{assignmentId} → Task&lt;Result&gt; RevokeAsync(int examId, int assignmentId, CancellationToken ct)
/// </code>
/// Ràng buộc phải cài (§5):
/// <list type="bullet">
/// <item><b>Khử trùng lặp ở backend</b> bằng <c>HashSet</c>:
/// <c>targets = distinct(studentIds hợp lệ ∪ học sinh của groupIds)</c>. Con số frontend tính chỉ
/// để hiện lên màn hình.</item>
/// <item><b>Idempotent</b> theo cặp <c>(examId, studentId)</c> có unique index. Giao lại cho người
/// đã có bài thì bỏ qua, giữ nguyên <c>status</c>/<c>attemptId</c>/<c>score</c>.</item>
/// <item><b>Kiểm tra phạm vi:</b> học sinh phải thuộc nhóm do chính giáo viên đó quản lý —
/// không cho giáo viên A giao bài cho lớp của giáo viên B bằng cách đoán id.</item>
/// <item><b>Học sinh bị khoá thì bỏ qua</b> và trả về <c>skippedLocked</c> để giáo viên biết;
/// frontend hiện vẫn cho chọn và vẫn tạo assignment.</item>
/// <item><b>Chặn giao đề đang là nháp</b> — frontend hoàn toàn không kiểm điều này.</item>
/// <item>Toàn bộ nằm trong một transaction: assignment + <c>ExamPaper</c> (khi bật
/// <c>randomizePerStudent</c>) + <c>Notification</c>; bắn <c>ExamAssigned</c> sau khi commit,
/// và chỉ khi <c>notify = true</c> (§7).</item>
/// <item><c>RevokeAsync</c> là bổ sung so với frontend: giao nhầm lớp là chuyện thường, mà
/// endpoint giao bài lại cố ý idempotent nên không có đường lùi. Từ chối thu hồi khi học sinh
/// đã bắt đầu làm bài.</item>
/// </list>
/// </remarks>
public interface IExamAssignmentService
{
}
