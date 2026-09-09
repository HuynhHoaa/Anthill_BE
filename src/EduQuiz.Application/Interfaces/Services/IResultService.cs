namespace EduQuiz.Application.Interfaces.Services;

/// <summary>Kết quả làm bài — §4.8. Dùng chung cho màn hình giáo viên và học sinh.</summary>
/// <remarks>
/// <b>Bản đồ đường đi</b> (chờ <c>DTOs/Results/</c>):
/// <code>
/// GET /api/results                     → Task&lt;Result&lt;PagedResult&lt;ResultSummaryDto&gt;&gt;&gt; ListAsync(ListResultsQuery query, CancellationToken ct)
/// GET /api/results/stats               → Task&lt;Result&lt;ResultStatsDto&gt;&gt; GetStatsAsync(ListResultsQuery query, CancellationToken ct)
/// GET /api/results/{attemptId}         → Task&lt;Result&lt;AttemptDetailDto&gt;&gt; GetDetailAsync(int attemptId, CancellationToken ct)
/// GET /api/results/{attemptId}/summary → Task&lt;Result&lt;ResultSummaryDto&gt;&gt; GetSummaryAsync(int attemptId, CancellationToken ct)
/// GET /api/my/results                  → Task&lt;Result&lt;PagedResult&lt;ResultSummaryDto&gt;&gt;&gt; ListMyResultsAsync(ListMyResultsQuery query, CancellationToken ct)
/// </code>
/// Ràng buộc phải cài:
/// <list type="bullet">
/// <item><b>Quyền xem đáp án thực thi ở server (§3 ⑥).</b> Giáo viên sở hữu đề luôn xem được.
/// Học sinh chỉ xem khi attempt là của mình <b>và</b> status đã là graded <b>và</b>
/// exam bật showResultAfterSubmit. Khi không đủ điều kiện, response phải <b>không chứa</b>
/// correctAnswerId hay isCorrect — ẩn ở giao diện là không đủ.</item>
/// <item><c>GetSummaryAsync</c> là bổ sung cho màn hình đã nộp bài: trang đó hiện gọi
/// getAttemptDetail, tức kéo về toàn bộ câu hỏi chỉ để hiện một vòng tròn điểm (§4.8).</item>
/// <item><c>GetStatsAsync</c> nên nhận cùng bộ lọc với <c>ListAsync</c>. Bản hiện tại không nhận
/// bộ lọc nên số liệu là của toàn hệ thống và không đổi theo bảng bên dưới, buộc frontend phải
/// ghi chú toàn hệ thống để tránh hiểu nhầm.</item>
/// <item>Học sinh gọi <c>GetDetailAsync</c> trên attempt không phải của mình thì trả
/// <see cref="Common.ErrorCode.NotFound"/>, không phải Forbidden.</item>
/// </list>
/// </remarks>
public interface IResultService
{
}
