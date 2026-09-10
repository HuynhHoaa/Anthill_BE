using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.Dashboard;

namespace EduQuiz.Application.Interfaces.Services;

/// <summary>Hai endpoint tổng quan gộp — §4.8.</summary>
/// <remarks>
/// Gộp chứ không tách: frontend gọi đúng một lần rồi dựng cả trang; tách ra sẽ thành sáu truy vấn
/// độc lập cho một màn hình.
///
/// Danh tính học sinh suy từ JWT (§3 ④); phạm vi của giáo viên giới hạn ở dữ liệu do chính mình
/// tạo, không phải toàn hệ thống.
/// </remarks>
public interface IDashboardService
{
    Task<Result<TeacherDashboardDto>> GetTeacherDashboardAsync(CancellationToken cancellationToken = default);

    Task<Result<StudentDashboardDto>> GetStudentDashboardAsync(CancellationToken cancellationToken = default);
}
