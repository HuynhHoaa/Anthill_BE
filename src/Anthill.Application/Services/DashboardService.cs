using Anthill.Application.Common;
using Anthill.Application.DTOs.Dashboard;
using Anthill.Application.DTOs.Exams;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

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
        // Chưa được gán môn thì CHỈ ô "tổng số câu hỏi" không tính được — sáu con số còn lại,
        // danh sách lớp và danh sách đề đều không liên quan gì tới môn. Chặn cả trang vì một ô là
        // dựng một bức tường trắng trước mặt một giáo viên mới, người còn chưa biết mình thiếu gì.
        // Ô đó trả 0, phần còn lại chạy bình thường.
        var subjectId = _currentUser.SubjectId;

        var now = DateTime.UtcNow;

        // Học sinh của giáo viên là học sinh trong những lớp họ được PHÂN CÔNG dạy, không phải
        // toàn trung tâm: con số "tổng học sinh" trên trang này phải là con số họ chịu trách nhiệm.
        // Lớp còn ở dạng nháp không được tính — quản trị viên vẫn đang xếp người vào đó.
        var groups = await _unitOfWork.StudentGroups.GetForTeacherAsync(CurrentUserId, search: null, cancellationToken);
        var groupSizes = await _unitOfWork.Users.CountStudentsByGroupAsync(
            groups.Select(group => group.Id),
            cancellationToken);

        var totalQuestions = subjectId is { } subject
            ? await _unitOfWork.Questions.CountInSubjectAsync(subject, cancellationToken)
            : 0;
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
            //
            // Gọi CanRevealScore chứ không chép lại quy tắc ba vế ra đây. Chép ra là tạo thêm một
            // bản sao phải nhớ cập nhật, và bản quên cập nhật ở đây chính là bản làm lộ điểm.
            // Phép kiểm cần Exam nằm sẵn trên Assignment — repository đã Include nó.
            if (attempt is not null)
            {
                attempt.Assignment = assignment;

                if (attempt.CanRevealScore(isOwningTeacher: false))
                {
                    visibleScores.Add(attempt.Score);
                }
            }
        }

        var (open, upcoming) = SplitOpenAndUpcoming(assignments, now);

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

                // Làm tròn giống AverageScore ngay bên trên. Không làm thì hai ô cạnh nhau hiện
                // "3,1" và "9,00" — cùng một thang điểm mà hai định dạng.
                HighestScore = visibleScores.Count > 0
                    ? Math.Round(visibleScores.Max(), 1, MidpointRounding.AwayFromZero)
                    : null
            },

            // Thứ tự đã chốt trong SplitOpenAndUpcoming: đang mở theo hạn nộp của lớp, sắp tới theo
            // giờ mở của lớp.
            OpenExams = open.Select(assignment => assignment.ToStudentExamItemDto(now)).ToList(),
            UpcomingExams = upcoming.Select(assignment => assignment.ToStudentExamItemDto(now)).ToList(),

            RecentResults = recentPage.Items
                .Select(attempt => attempt.ToSummaryDto(
                    attempt.CanRevealScore(isOwningTeacher: false),
                    questionCounts.GetValueOrDefault(attempt.Id)))
                .ToList()
        });
    }

    /// <summary>
    /// Chia bài được giao của một học sinh thành "đang mở" và "sắp tới", mỗi nhóm đã sắp sẵn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vòng đời và hai mốc giờ lấy từ BẢN GHI GIAO BÀI, không từ đề — đúng như chính thẻ bài bên trong
    /// (<see cref="ExamAttemptMappingExtensions.ToStudentExamItemDto"/>) và như <c>GET /api/my/exams</c>.
    /// Đọc lịch của đề ở đây là để trang tổng quan nói ngược với thẻ nó đang vẽ: lớp chiều đang trong
    /// giờ mà bài biến khỏi cả hai danh sách vì lịch chung đã đóng, còn lớp sáng đã hết giờ lại nằm
    /// trong "cần làm ngay".
    /// </para>
    /// <para>Hàm tĩnh thuần để kiểm được bằng unit test mà không phải dựng cả IUnitOfWork.</para>
    /// </remarks>
    public static (IReadOnlyList<ExamAssignment> Open, IReadOnlyList<ExamAssignment> Upcoming) SplitOpenAndUpcoming(
        IEnumerable<ExamAssignment> assignments,
        DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        var open = new List<(ExamAssignment Assignment, DateTime CloseAt)>();
        var upcoming = new List<(ExamAssignment Assignment, DateTime OpenAt)>();

        foreach (var assignment in assignments)
        {
            // Bài đã NỘP thì không còn nằm ở "đang mở" nữa. Nhưng bài đang làm dở thì có: đó
            // chính là việc em cần làm gấp nhất, và bỏ nó ra khỏi cả hai danh sách nghĩa là trang
            // tổng quan không còn đường nào dẫn em quay lại bài đang chạy đồng hồ.
            if (assignment.Attempt is { Status: not AssignmentStatus.InProgress })
            {
                continue;
            }

            var (openAt, closeAt) = AssignmentSchedule.Resolve(assignment);

            switch (assignment.ResolveLifecycle(asOfUtc))
            {
                case ExamLifecycle.Open:
                    open.Add((assignment, closeAt.ToUtc()));
                    break;
                case ExamLifecycle.Scheduled:
                    upcoming.Add((assignment, openAt.ToUtc()));
                    break;
            }
        }

        // Sắp theo mốc THẬT của em: frontend lấy phần tử đầu của "đang mở" làm cảnh báo sắp hết hạn,
        // nên sắp theo lịch chung của đề là chỉ nhầm bài. OrderBy ổn định: hai bài cùng mốc giữ thứ tự
        // repository (mới giao trước).
        return (
            open.OrderBy(entry => entry.CloseAt).Select(entry => entry.Assignment).ToList(),
            upcoming.OrderBy(entry => entry.OpenAt).Select(entry => entry.Assignment).ToList());
    }
}
