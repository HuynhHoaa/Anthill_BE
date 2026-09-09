using EduQuiz.Application.DTOs.ExamAttempts;
using EduQuiz.Application.DTOs.Results;

namespace EduQuiz.Application.DTOs.Dashboard;

/// <summary>Trang tổng quan của học sinh — một lời gọi cho cả màn hình.</summary>
/// <remarks>
/// Danh tính suy từ token, không nhận <c>studentId</c> từ client (§3 quy tắc ④).
/// </remarks>
public sealed record StudentDashboardDto
{
    public required StudentDashboardStatsDto Stats { get; init; }

    /// <summary>Bài đang mở, làm được ngay bây giờ. Đây là thứ em cần thấy đầu tiên.</summary>
    public required IReadOnlyList<StudentExamItemDto> OpenExams { get; init; }

    /// <summary>Bài đã giao nhưng chưa tới giờ mở.</summary>
    public required IReadOnlyList<StudentExamItemDto> UpcomingExams { get; init; }

    /// <summary>Vài kết quả gần nhất. Điểm chỉ có mặt khi giáo viên cho xem.</summary>
    public required IReadOnlyList<ResultSummaryDto> RecentResults { get; init; }
}

/// <summary>Bốn con số đầu trang tổng quan của học sinh.</summary>
public sealed record StudentDashboardStatsDto
{
    public required int AssignedExams { get; init; }

    /// <summary>Số lượt ở trạng thái submitted hoặc graded (§5).</summary>
    public required int CompletedExams { get; init; }

    /// <summary>
    /// Điểm trung bình của chính em, trên các lượt đã chấm, làm tròn một chữ số.
    /// </summary>
    /// <remarks>
    /// Chỉ tính những bài mà giáo viên đã cho xem điểm. Một con số trung bình gộp cả điểm em chưa
    /// được phép biết là cách để lộ điểm bằng phép trừ: làm thêm một bài rồi so hai lần trung bình
    /// là suy ra được điểm bài vừa giấu.
    /// </remarks>
    public decimal? AverageScore { get; init; }

    /// <summary>Điểm cao nhất trong các bài đã được cho xem điểm. NULL cùng lúc với <see cref="AverageScore"/>.</summary>
    public decimal? HighestScore { get; init; }
}
