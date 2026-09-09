using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.Dashboard;
using EduQuiz.Application.DTOs.Exams;
using EduQuiz.Application.Interfaces.Security;
using EduQuiz.Application.Interfaces.Services;
using EduQuiz.Application.Interfaces.UnitOfWork;
using EduQuiz.Application.Mappings;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Services;

/// <summary>
/// Cài đặt <see cref="IDashboardService"/> — hai endpoint tổng quan gộp của §4.8.
/// </summary>
/// <remarks>
/// Hai điều quyết định nằm ở file này:
/// <list type="number">
/// <item><b>Gộp là mục đích, không phải tiện tay.</b> Mỗi trang tổng quan là một lời gọi. Tách
/// thành sáu endpoint nhỏ nghe có vẻ sạch hơn, nhưng đó là sáu lần chịu độ trễ cho một màn hình,
/// và trên dịch vụ miễn phí có cold start thì cả sáu cùng đánh thức tiến trình.</item>
/// <item><b>Điểm trung bình của học sinh chỉ tính trên bài đã được cho xem điểm.</b> Gộp cả điểm
/// em chưa được phép biết vào một con số trung bình là cách để lộ điểm bằng phép trừ: làm thêm một
/// bài rồi so hai lần trung bình là suy ra được điểm bài vừa giấu.</item>
/// </list>
/// </remarks>
public sealed class DashboardService : IDashboardService
{
    private const string NoSubjectMessage =
        "Tài khoản giáo viên chưa được gán môn nên chưa có ngân hàng câu hỏi nào để thống kê.";

    /// <summary>Số đề gần đây hiện trên trang tổng quan của giáo viên.</summary>
    private const int RecentExamCount = 5;

    /// <summary>Số kết quả gần nhất hiện trên trang tổng quan của học sinh.</summary>
    private const int RecentResultCount = 5;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public DashboardService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    private int CurrentUserId => _currentUser.RequireUserId();

    /// <inheritdoc />
    public async Task<Result<TeacherDashboardDto>> GetTeacherDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.SubjectId is not { } subjectId)
        {
            return Result.Forbidden<TeacherDashboardDto>(NoSubjectMessage);
        }

        var now = DateTime.UtcNow;

        // Học sinh của giáo viên là học sinh trong các nhóm lớp do chính họ quản lý, không phải
        // toàn trường: con số "tổng học sinh" trên trang này phải là con số họ chịu trách nhiệm.
        var groups = await _unitOfWork.StudentGroups.GetOwnedAsync(CurrentUserId, search: null, cancellationToken);
        var groupSizes = await _unitOfWork.Users.CountStudentsByGroupAsync(
            groups.Select(group => group.Id),
            cancellationToken);

        var totalQuestions = await _unitOfWork.Questions.CountInSubjectAsync(subjectId, cancellationToken);
        var totalExams = await _unitOfWork.Exams.CountByCreatorAsync(CurrentUserId, cancellationToken);

        // Đề đang mở: lọc ngay trong SQL theo cùng mốc thời gian, không đọc hết rồi lọc trong bộ nhớ.
        var openPage = await _unitOfWork.Exams.GetPagedAsync(
            CurrentUserId, search: null, ExamLifecycle.Open, now,
            PaginationParams.Of(1, 1), cancellationToken);

        var assignedExams = await _unitOfWork.Exams.CountAssignedByCreatorAsync(CurrentUserId, cancellationToken);

        var submissionsThisWeek = await _unitOfWork.ExamAttempts.CountSubmittedSinceAsync(
            CurrentUserId, now.AddDays(-7), cancellationToken);

        // Dùng lại đúng phép tổng hợp của trang Kết quả, không lọc gì: hai màn hình nói về cùng
        // một tập dữ liệu thì phải cho ra cùng một con số.
        var stats = await _unitOfWork.ExamAttempts.GetResultStatsAsync(
            CurrentUserId, null, null, null, null, null, cancellationToken);

        var recent = await _unitOfWork.Exams.GetRecentAsync(CurrentUserId, RecentExamCount, cancellationToken);
        var counters = await _unitOfWork.ExamAssignments.CountByExamsAsync(
            recent.Select(exam => exam.Id), cancellationToken);

        return Result.Success(new TeacherDashboardDto
        {
            Stats = new TeacherDashboardStatsDto
            {
                TotalStudents = groupSizes.Values.Sum(),
                TotalQuestions = totalQuestions,
                TotalExams = totalExams,
                AssignedExams = assignedExams,
                OpenExams = openPage.Total,
                SubmissionsThisWeek = submissionsThisWeek,
                AverageScore = stats.GradedCount > 0 ? stats.AverageScore : null
            },

            RecentExams = recent.Select(exam =>
            {
                var counts = counters.TryGetValue(exam.Id, out var value) ? value : (Assigned: 0, Submitted: 0);
                return exam.ToDto(now, counts.Assigned, counts.Submitted);
            }).ToList()
        });
    }

    /// <inheritdoc />
    public async Task<Result<StudentDashboardDto>> GetStudentDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Một lời gọi lấy trọn dữ liệu của em này — bài được giao, đề kèm theo, lượt làm bài nếu
        // có. Bốn con số và ba danh sách phía dưới đều rút ra từ đây, nên chúng chắc chắn nhất
        // quán với nhau; hỏi database bảy lần thì bảy câu trả lời có thể thuộc bảy thời điểm.
        var assignments = await _unitOfWork.ExamAssignments.GetForStudentAsync(CurrentUserId, cancellationToken);

        var open = new List<ExamAssignment>();
        var upcoming = new List<ExamAssignment>();
        var visibleScores = new List<decimal>();
        var completed = 0;

        foreach (var assignment in assignments)
        {
            var attempt = assignment.Attempt;

            if (attempt is { Status: AssignmentStatus.Submitted or AssignmentStatus.Graded })
            {
                completed++;
            }

            // Chỉ gom điểm mà em được phép nhìn thấy. Xem chú thích ở đầu lớp: một con số trung
            // bình gộp cả điểm đang giấu là điểm bị lộ qua phép trừ.
            if (attempt is { Status: AssignmentStatus.Graded } && assignment.Exam.ShowResultAfterSubmit)
            {
                visibleScores.Add(attempt.Score);
            }

            // Bài đã làm rồi thì không còn nằm ở "đang mở" nữa — chỗ đó dành cho việc em cần làm.
            if (attempt is not null)
            {
                continue;
            }

            switch (assignment.Exam.ResolveLifecycle(now))
            {
                case ExamLifecycle.Open:
                    open.Add(assignment);
                    break;
                case ExamLifecycle.Scheduled:
                    upcoming.Add(assignment);
                    break;
            }
        }

        var recentPage = await _unitOfWork.ExamAttempts.GetMyResultsPagedAsync(
            CurrentUserId, search: null, PaginationParams.Of(1, RecentResultCount), cancellationToken);

        var questionCounts = await _unitOfWork.ExamAttempts.GetPaperQuestionCountsAsync(
            recentPage.Items.Select(attempt => attempt.Id), cancellationToken);

        return Result.Success(new StudentDashboardDto
        {
            Stats = new StudentDashboardStatsDto
            {
                AssignedExams = assignments.Count,
                CompletedExams = completed,

                AverageScore = visibleScores.Count > 0
                    ? Math.Round(visibleScores.Average(), 1, MidpointRounding.AwayFromZero)
                    : null,

                HighestScore = visibleScores.Count > 0 ? visibleScores.Max() : null
            },

            // Sắp theo hạn nộp gần nhất: bài sắp đóng là bài cần làm trước.
            OpenExams = open
                .OrderBy(assignment => assignment.Exam.CloseAt)
                .Select(assignment => assignment.ToStudentExamItemDto(now))
                .ToList(),

            // Sắp theo giờ mở: bài sắp tới trước.
            UpcomingExams = upcoming
                .OrderBy(assignment => assignment.Exam.OpenAt)
                .Select(assignment => assignment.ToStudentExamItemDto(now))
                .ToList(),

            RecentResults = recentPage.Items
                .Select(attempt => attempt.ToSummaryDto(
                    attempt.CanRevealScore(isOwningTeacher: false),
                    questionCounts.GetValueOrDefault(attempt.Id)))
                .ToList()
        });
    }
}
