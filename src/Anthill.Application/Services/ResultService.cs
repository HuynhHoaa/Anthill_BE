using Anthill.Application.Common;
using Anthill.Application.DTOs.Results;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

/// <summary>
/// Cài đặt <see cref="IResultService"/> — năm endpoint của §4.8.
/// </summary>
/// <remarks>
/// Ba điều quyết định nằm ở file này:
/// <list type="number">
/// <item><b>Cùng một endpoint, hai mức chi tiết.</b> Giáo viên và học sinh gọi chung
/// <c>GET /api/results/{attemptId}</c>, nhưng thứ trả về khác nhau: quyền xem đáp án tính ở server
/// rồi <b>cắt hẳn</b> khỏi JSON, không phải gửi đi rồi nhờ giao diện giấu hộ (§3 ⑥). Với học sinh,
/// điểm và đáp án mở ở hai thời điểm: điểm ngay khi chấm, đáp án khi mọi lớp đã hết giờ.</item>
/// <item><b>Bài của người khác trả <c>NotFound</c>, không phải <c>Forbidden</c></b> — 403 là xác
/// nhận id đó có thật (§4.2).</item>
/// <item><b>Chi tiết bài làm đọc từ bản đề, không từ ngân hàng câu hỏi.</b> Câu học sinh bỏ trống
/// không có dòng nào trong bảng câu trả lời, nên nếu duyệt theo câu trả lời thì đề 40 câu mà em
/// làm 30 sẽ hiện ra thành bài 30 câu — mất dấu đúng những câu em bỏ.</item>
/// </list>
/// </remarks>
public sealed class ResultService : IResultService
{
    private const string NotFoundMessage = "Không tìm thấy bài làm này.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public ResultService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    private int CurrentUserId => _currentUser.RequireUserId();

    private bool IsStudent => _currentUser.IsInRole(UserRole.Student);

    /// <inheritdoc />
    public async Task<Result<PagedResult<ResultSummaryDto>>> ListAsync(
        ListResultsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!FilterValueParser.TryParseFilter<AssignmentStatus>(query.Status, out var status))
        {
            return Result.Failure<PagedResult<ResultSummaryDto>>(
                "Bộ lọc trạng thái chỉ nhận 'assigned', 'inprogress', 'submitted', 'graded', 'expired' hoặc 'all'.");
        }

        var page = await _unitOfWork.ExamAttempts.GetResultsPagedAsync(
            CurrentUserId,
            query.Search,
            query.ExamId,
            query.StudentId,
            query.GroupId,
            status,
            query,
            cancellationToken);

        // Người gọi là giáo viên sở hữu mọi dòng ở đây — repository đã lọc theo CreatedByUserId —
        // nên không cần hỏi lại quyền xem điểm cho từng dòng.
        var counts = await LoadQuestionCountsAsync(page.Items, cancellationToken);

        return Result.Success(page.Map(attempt =>
            attempt.ToSummaryDto(canRevealScore: true, counts.GetValueOrDefault(attempt.Id))));
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<ResultSummaryDto>>> ListMyResultsAsync(
        ListMyResultsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = await _unitOfWork.ExamAttempts.GetMyResultsPagedAsync(
            CurrentUserId,
            query.Search,
            query,
            cancellationToken);

        var counts = await LoadQuestionCountsAsync(page.Items, cancellationToken);

        return Result.Success(page.Map(attempt =>
            attempt.ToSummaryDto(
                attempt.CanRevealScore(isOwningTeacher: false),
                counts.GetValueOrDefault(attempt.Id))));
    }

    /// <inheritdoc />
    public async Task<Result<ResultStatsDto>> GetStatsAsync(
        ListResultsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!FilterValueParser.TryParseFilter<AssignmentStatus>(query.Status, out var status))
        {
            return Result.Failure<ResultStatsDto>(
                "Bộ lọc trạng thái chỉ nhận 'assigned', 'inprogress', 'submitted', 'graded', 'expired' hoặc 'all'.");
        }

        var stats = await _unitOfWork.ExamAttempts.GetResultStatsAsync(
            CurrentUserId,
            query.Search,
            query.ExamId,
            query.StudentId,
            query.GroupId,
            status,
            cancellationToken);

        return Result.Success(new ResultStatsDto
        {
            TotalAttempts = stats.TotalAttempts,
            GradedCount = stats.GradedCount,

            // NULL chứ không phải 0 khi chưa chấm bài nào. Trả 0 thì màn hình hiện "trung bình 0
            // điểm" cho một lớp chưa ai thi, và giáo viên đọc con số đó sẽ tưởng cả lớp làm sai hết.
            AverageScore = stats.GradedCount > 0 ? stats.AverageScore : null,
            HighestScore = stats.GradedCount > 0 ? stats.HighestScore : null,

            CompletionRate = stats.AssignedCount > 0
                ? Math.Round(stats.SubmittedCount * 100m / stats.AssignedCount, 1, MidpointRounding.AwayFromZero)
                : 0m
        });
    }

    /// <inheritdoc />
    public async Task<Result<ResultSummaryDto>> GetSummaryAsync(
        int attemptId,
        CancellationToken cancellationToken = default)
    {
        var found = await FindReadableAttemptAsync(attemptId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<ResultSummaryDto>(found.Error!, found.ErrorCode);
        }

        var (attempt, isOwningTeacher) = found.ValueOrThrow;
        var counts = await LoadQuestionCountsAsync([attempt], cancellationToken);

        return Result.Success(attempt.ToSummaryDto(
            attempt.CanRevealScore(isOwningTeacher),
            counts.GetValueOrDefault(attempt.Id)));
    }

    /// <inheritdoc />
    public async Task<Result<AttemptDetailDto>> GetDetailAsync(
        int attemptId,
        CancellationToken cancellationToken = default)
    {
        var found = await FindReadableAttemptAsync(attemptId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<AttemptDetailDto>(found.Error!, found.ErrorCode);
        }

        var (attempt, isOwningTeacher) = found.ValueOrThrow;

        var withAnswers = await _unitOfWork.ExamAttempts.GetWithAnswersAsync(attemptId, cancellationToken);
        if (withAnswers is null)
        {
            return Result.NotFound<AttemptDetailDto>(NotFoundMessage);
        }

        var paper = await ResolvePaperAsync(withAnswers.Assignment, cancellationToken);
        if (paper is null)
        {
            return Result.Failure<AttemptDetailDto>(
                "Không tìm thấy bản đề của bài làm này.", ErrorCode.Conflict);
        }

        var canSeeScore = attempt.CanRevealScore(isOwningTeacher);

        // Mốc mở đáp án là giờ đóng muộn nhất trong MỌI lượt giao của đề, không phải giờ đóng của lớp
        // em này: lớp làm muộn nhất chưa xong thì đáp án chưa được rời server. Vế dự phòng chỉ để giữ
        // kiểu không NULL — chính lượt giao của em đã là một dòng trong phép MAX.
        var releaseAt = await _unitOfWork.ExamAssignments.GetAnswersReleaseAtAsync(
                            attempt.Assignment.ExamId, cancellationToken)
                        ?? AssignmentSchedule.Resolve(attempt.Assignment).CloseAt;
        var answers = attempt.ResolveAnswerVisibility(isOwningTeacher, releaseAt, DateTime.UtcNow);

        // Tra cứu theo câu, không duyệt theo câu trả lời: câu học sinh bỏ trống không có dòng nào
        // trong bảng StudentAnswer, nên duyệt theo đó thì đề 40 câu mà em làm 30 sẽ hiện ra thành
        // bài 30 câu — mất dấu đúng những câu đáng chú ý nhất.
        var selections = withAnswers.Answers
            .GroupBy(answer => answer.ExamPaperQuestionId)
            .ToDictionary(group => group.Key, group => group.First().SelectedAnswerId);

        var questions = paper.Questions
            .OrderBy(question => question.Ordinal)
            .Select(question => ToDetailQuestion(question, selections, answers.CanSee))
            .ToList();

        var assignment = withAnswers.Assignment;
        var student = assignment.Student;

        return Result.Success(new AttemptDetailDto
        {
            AttemptId = withAnswers.Id,
            ExamId = assignment.ExamId,
            ExamTitle = assignment.Exam.Title,
            Topic = assignment.Exam.Topic ?? string.Empty,
            StudentId = assignment.StudentId,
            StudentName = student?.FullName ?? string.Empty,
            StudentCode = student?.Code ?? string.Empty,
            Status = withAnswers.Status.ToAssignmentStatusString(),
            StartedAt = withAnswers.StartedAt.ToUtc(),
            SubmittedAt = withAnswers.SubmittedAt.ToUtc(),
            AutoSubmitted = withAnswers.AutoSubmitted,

            DurationUsedMinutes = withAnswers.SubmittedAt is { } submittedAt
                ? (int)Math.Round((submittedAt - withAnswers.StartedAt).TotalMinutes, MidpointRounding.AwayFromZero)
                : null,

            // Điểm và đáp án đi hai cổng khác nhau: điểm mở ngay khi chấm, đáp án chờ lớp cuối cùng.
            Score = canSeeScore ? withAnswers.Score : null,
            MaxScore = canSeeScore ? withAnswers.MaxScore : null,
            CorrectCount = canSeeScore ? withAnswers.CorrectCount : null,
            CanSeeAnswers = answers.CanSee,
            AnswersVisibleAt = answers.VisibleAt,
            Questions = questions
        });
    }

    /// <summary>
    /// Một câu trong bài đã làm. Đáp án đúng chỉ được gắn vào khi người gọi đủ điều kiện xem.
    /// </summary>
    /// <remarks>
    /// Hai trường <c>CorrectAnswerId</c> và <c>IsCorrect</c> để NULL thì biến mất khỏi JSON nhờ
    /// cấu hình bỏ qua giá trị null toàn cục. Đó là điều khác biệt then chốt so với việc trả về
    /// rồi nhờ giao diện giấu: dữ liệu không rời server thì không ai mở tab Network ra xem được.
    /// </remarks>
    private static AttemptDetailQuestionDto ToDetailQuestion(
        ExamPaperQuestion question,
        IReadOnlyDictionary<int, int?> selections,
        bool canSeeAnswers)
    {
        var correct = question.Answers.FirstOrDefault(answer => answer.IsCorrect);
        var selected = selections.TryGetValue(question.Id, out var value) ? value : null;

        return new AttemptDetailQuestionDto
        {
            PaperQuestionId = question.Id,
            Ordinal = question.Ordinal,
            Content = question.ContentSnapshot,
            ImageUrl = question.ImageUrlSnapshot,
            Point = question.Point,

            Answers = question.Answers
                .OrderBy(answer => answer.Ordinal)
                .Select(answer => new AttemptDetailAnswerDto
                {
                    Id = answer.Id,
                    Ordinal = answer.Ordinal,
                    Content = answer.ContentSnapshot,
                    ImageUrl = answer.ImageUrlSnapshot
                })
                .ToList(),

            SelectedAnswerId = selected,
            CorrectAnswerId = canSeeAnswers ? correct?.Id : null,
            IsCorrect = canSeeAnswers ? selected is not null && selected == correct?.Id : null
        };
    }

    /// <summary>
    /// Lượt làm bài mà người gọi được đọc, kèm cờ "người gọi có phải giáo viên sở hữu đề không".
    /// </summary>
    /// <remarks>
    /// Cả ba nhánh hỏng — không tồn tại, không phải bài của mình, không phải đề của mình — đều trả
    /// cùng một <c>NotFound</c> với cùng một câu chữ. Phân biệt chúng là để lộ rằng id nào có thật,
    /// đủ để dò dần toàn bộ bảng kết quả của người khác (§4.2).
    /// </remarks>
    private async Task<Result<(ExamAttempt Attempt, bool IsOwningTeacher)>> FindReadableAttemptAsync(
        int attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await _unitOfWork.ExamAttempts.GetSessionAsync(attemptId, cancellationToken);
        if (attempt is null)
        {
            return Result.NotFound<(ExamAttempt, bool)>(NotFoundMessage);
        }

        if (IsStudent)
        {
            return attempt.Assignment.StudentId == CurrentUserId
                ? Result.Success((attempt, false))
                : Result.NotFound<(ExamAttempt, bool)>(NotFoundMessage);
        }

        // Giáo viên và quản trị viên: chỉ đề do chính mình tạo. Quản trị viên không tạo đề nào nên
        // nhánh này tự nhiên loại họ ra, đúng như §4.8 xếp bảng kết quả vào phần của giáo viên.
        return attempt.Assignment.Exam.CreatedByUserId == CurrentUserId
            ? Result.Success((attempt, true))
            : Result.NotFound<(ExamAttempt, bool)>(NotFoundMessage);
    }

    /// <summary>Bản đề của một assignment: bản riêng nếu có, ngược lại bản chung của cả lớp.</summary>
    private async Task<ExamPaper?> ResolvePaperAsync(ExamAssignment assignment, CancellationToken cancellationToken)
    {
        var paper = await _unitOfWork.Exams.GetPaperByAssignmentAsync(assignment.Id, cancellationToken)
                    ?? await _unitOfWork.Exams.GetSharedPaperAsync(assignment.ExamId, cancellationToken);

        return paper is null
            ? null
            : await _unitOfWork.Exams.GetPaperWithQuestionsAsync(paper.Id, cancellationToken);
    }

    /// <summary>Số câu của bản đề ứng với từng lượt, lấy một lần cho cả trang.</summary>
    private async Task<IReadOnlyDictionary<int, int>> LoadQuestionCountsAsync(
        IReadOnlyList<ExamAttempt> attempts,
        CancellationToken cancellationToken) =>
        attempts.Count == 0
            ? new Dictionary<int, int>()
            : await _unitOfWork.ExamAttempts.GetPaperQuestionCountsAsync(
                attempts.Select(attempt => attempt.Id),
                cancellationToken);
}
