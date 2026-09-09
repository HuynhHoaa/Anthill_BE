namespace EduQuiz.Application.Interfaces.Services;

/// <summary>Hai endpoint tổng quan gộp — §4.8.</summary>
/// <remarks>
/// <b>Bản đồ đường đi</b> (chờ <c>DTOs/Dashboard/</c>):
/// <code>
/// GET /api/dashboard/teacher → Task&lt;Result&lt;TeacherDashboardDto&gt;&gt; GetTeacherDashboardAsync(CancellationToken ct)
/// GET /api/dashboard/student → Task&lt;Result&lt;StudentDashboardDto&gt;&gt; GetStudentDashboardAsync(CancellationToken ct)
/// </code>
/// Hai endpoint này <b>nên gộp, không nên tách</b>: frontend gọi đúng một lần rồi render cả
/// trang; tách ra sẽ thành sáu truy vấn độc lập cho một màn hình (§4.8).
/// <para>
/// Giáo viên nhận stats (tổng học sinh, tổng câu hỏi, tổng đề, đề đã giao, đề đang mở, số bài
/// nộp trong tuần, điểm trung bình) kèm recentExams và activities.
/// Học sinh nhận stats (số bài được giao, số bài đã hoàn thành, điểm trung bình, điểm cao nhất)
/// kèm openExams, upcomingExams và recentResults.
/// </para>
/// Danh tính học sinh suy từ JWT (§3 ④); phạm vi của giáo viên giới hạn ở dữ liệu do chính
/// mình tạo, không phải toàn hệ thống.
/// </remarks>
public interface IDashboardService
{
}
