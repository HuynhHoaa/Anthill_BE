using EduQuiz.Application.DTOs.Exams;

namespace EduQuiz.Application.DTOs.Dashboard;

/// <summary>Trang tổng quan của giáo viên — một lời gọi cho cả màn hình.</summary>
/// <remarks>
/// Gộp chứ không tách. Frontend gọi đúng một lần rồi dựng cả trang; tách ra thành sáu endpoint là
/// sáu lần chịu độ trễ cho một màn hình, và trên dịch vụ miễn phí có cold start thì cả sáu cùng
/// đánh thức tiến trình (§4.8).
///
/// Phạm vi là dữ liệu của <b>chính giáo viên đó</b>, không phải toàn hệ thống.
/// </remarks>
public sealed record TeacherDashboardDto
{
    public required TeacherDashboardStatsDto Stats { get; init; }

    /// <summary>Vài đề gần đây nhất, cho khối "Bài kiểm tra gần đây".</summary>
    public required IReadOnlyList<ExamDto> RecentExams { get; init; }
}

/// <summary>Bảy con số đầu trang tổng quan của giáo viên.</summary>
public sealed record TeacherDashboardStatsDto
{
    /// <summary>Số học sinh thuộc các nhóm lớp do giáo viên này quản lý.</summary>
    public required int TotalStudents { get; init; }

    /// <summary>Số câu hỏi trong ngân hàng mà giáo viên này nhìn thấy được (§6).</summary>
    public required int TotalQuestions { get; init; }

    public required int TotalExams { get; init; }

    /// <summary>Số đề đã giao cho ít nhất một học sinh.</summary>
    public required int AssignedExams { get; init; }

    /// <summary>Số đề đang trong khoảng mở, tính theo giờ server lúc đọc.</summary>
    public required int OpenExams { get; init; }

    /// <summary>Số bài học sinh nộp trong bảy ngày gần nhất.</summary>
    public required int SubmissionsThisWeek { get; init; }

    /// <summary>
    /// Điểm trung bình trên các lượt đã chấm của mọi đề do giáo viên này tạo.
    /// NULL khi chưa có bài nào được chấm.
    /// </summary>
    public decimal? AverageScore { get; init; }
}
