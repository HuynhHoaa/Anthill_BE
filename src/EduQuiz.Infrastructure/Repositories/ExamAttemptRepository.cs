using EduQuiz.Application.Common;
using EduQuiz.Application.Interfaces.Repositories;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;
using EduQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduQuiz.Infrastructure.Repositories;

public class ExamAttemptRepository : GenericRepository<ExamAttempt>, IExamAttemptRepository
{
    public ExamAttemptRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ExamAttempt?> GetByAssignmentAsync(
        int assignmentId,
        CancellationToken cancellationToken = default) =>
        await DbSet.FirstOrDefaultAsync(a => a.AssignmentId == assignmentId, cancellationToken);

    /// <summary>
    /// Đủ ngữ cảnh để dựng lại phiên làm bài sau F5. Nội dung bản đề <b>không</b> nạp ở đây:
    /// đề chung của cả lớp có <c>AssignmentId = NULL</c> nên không đi tới được qua navigation này.
    /// Cả hai trường hợp đều đi qua <c>IExamRepository.GetPaperWithQuestionsAsync</c> để service
    /// chỉ có một đường lấy câu hỏi.
    /// </summary>
    public async Task<ExamAttempt?> GetSessionAsync(int attemptId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(a => a.Assignment)
                .ThenInclude(assignment => assignment.Exam)
            .Include(a => a.Assignment)
                .ThenInclude(assignment => assignment.Student)
            .Include(a => a.Assignment)
                .ThenInclude(assignment => assignment.Paper)
            .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken);

    public async Task<ExamAttempt?> GetWithAnswersAsync(int attemptId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(a => a.Assignment)
                .ThenInclude(assignment => assignment.Exam)
            .Include(a => a.Assignment)
                .ThenInclude(assignment => assignment.Student)
            .Include(a => a.Answers)
                .ThenInclude(answer => answer.ExamPaperQuestion)
                    .ThenInclude(question => question.Answers.OrderBy(option => option.Ordinal))
            .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken);

    /// <summary>
    /// Trả NULL khi lượt làm bài không tồn tại. Service trả 404 cho cả "không có" lẫn
    /// "không phải của mình" để không ai dò được id hợp lệ (§4.2).
    /// </summary>
    public async Task<int?> GetOwnerStudentIdAsync(int attemptId, CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(a => a.Id == attemptId)
            .Select(a => (int?)a.Assignment.StudentId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// <c>FOR UPDATE</c> của PostgreSQL: request thứ hai chạm vào cùng một lượt phải chờ tới khi
    /// request đầu commit rồi mới đọc, và lúc đó nó đọc được bản vừa ghi chứ không phải bản cũ. Đó
    /// là thứ biến chuỗi đọc–quyết định–ghi ở tầng service thành một thao tác nguyên tử mà không
    /// phải thêm cột phiên bản nào vào lược đồ.
    /// </remarks>
    public async Task<ExamAttempt?> LockForUpdateAsync(int attemptId, CancellationToken cancellationToken = default)
    {
        // Phải hỏi trước khi truy vấn, vì ngay sau nó thì dòng nào cũng đã nằm trong change tracker.
        var trackedBefore = Context.ChangeTracker
            .Entries<ExamAttempt>()
            .Any(entry => entry.Entity.Id == attemptId);

        // Không nối thêm toán tử LINQ nào phía sau, để EF gửi thẳng câu lệnh này xuống thay vì bọc
        // nó vào một truy vấn con.
        var rows = await DbSet
            .FromSql($"""SELECT * FROM "ExamAttempts" WHERE "Id" = {attemptId} FOR UPDATE""")
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var attempt = rows[0];

        if (trackedBefore)
        {
            // Với thực thể đã theo dõi từ trước, EF trả lại chính đối tượng cũ và giữ nguyên giá trị
            // của lần đọc trước — tức là ảnh chụp từ trước lúc phải xếp hàng chờ khoá, đúng thứ
            // không được phép đem ra quyết định. Đọc lại ở đây an toàn vì dòng đã bị khoá: không ai
            // chen vào giữa hai câu lệnh được nữa.
            await Context.Entry(attempt).ReloadAsync(cancellationToken);
        }

        return attempt;
    }

    /// <summary>
    /// Bài đang làm dở đã quá hạn. Học sinh tắt trình duyệt thì không ai gọi submit, nên
    /// background job phải tự đi tìm (§3 quy tắc ③). Lấy theo lô để một lần quét không kéo
    /// cả nghìn bản ghi về bộ nhớ.
    /// </summary>
    /// <remarks>
    /// Nạp kèm hồ sơ học sinh vì việc tự nộp còn sinh thông báo cho giáo viên, mà thông báo đó phải
    /// gọi tên em nào hết giờ. Thiếu <c>Student</c> ở đây thì thông báo vẫn gửi được nhưng nội dung
    /// rỗng nghĩa — và đó là kiểu hỏng chỉ lộ ra trên môi trường thật, nơi background job mới chạy.
    /// </remarks>
    public async Task<IReadOnlyList<ExamAttempt>> GetExpiredInProgressAsync(
        DateTime utcNow,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            return [];
        }

        return await DbSet
            .Include(a => a.Assignment)
                .ThenInclude(assignment => assignment.Exam)
            .Include(a => a.Assignment)
                .ThenInclude(assignment => assignment.Student)
            .Where(a => a.Status == AssignmentStatus.InProgress && a.ExpiresAt <= utcNow)
            .OrderBy(a => a.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExamAttempt>> GetDeadlineSoonAsync(
        DateTime utcNow,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var until = utcNow.Add(window);

        return await DbSet
            .AsNoTracking()
            .Include(a => a.Assignment)
                .ThenInclude(assignment => assignment.Exam)
            .Include(a => a.Assignment)
                .ThenInclude(assignment => assignment.Student)
            .Where(a => a.Status == AssignmentStatus.InProgress && a.ExpiresAt > utcNow && a.ExpiresAt <= until)
            .OrderBy(a => a.ExpiresAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<ExamAttempt>> GetResultsPagedAsync(
        int teacherId,
        string? search,
        int? examId,
        int? studentId,
        int? groupId,
        AssignmentStatus? status,
        PaginationParams pagination,
        CancellationToken cancellationToken = default)
    {
        // Giáo viên chỉ thấy kết quả của đề do chính mình tạo.
        var query = ResultsQuery().Where(a => a.Assignment.Exam.CreatedByUserId == teacherId);

        if (examId.HasValue)
        {
            query = query.Where(a => a.Assignment.ExamId == examId.Value);
        }

        if (studentId.HasValue)
        {
            query = query.Where(a => a.Assignment.StudentId == studentId.Value);
        }

        if (groupId.HasValue)
        {
            query = query.Where(a => a.Assignment.Student.GroupId == groupId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        if (SearchText.ToLikePattern(search) is { } pattern)
        {
            query = query.Where(a =>
                EF.Functions.ILike(EF.Functions.Unaccent(a.Assignment.Student.FullName), pattern)
                || EF.Functions.ILike(a.Assignment.Student.Code ?? string.Empty, pattern)
                || EF.Functions.ILike(EF.Functions.Unaccent(a.Assignment.Exam.Title), pattern));
        }

        return await OrderForDisplay(query).ToPagedResultAsync(pagination, cancellationToken);
    }

    public async Task<PagedResult<ExamAttempt>> GetMyResultsPagedAsync(
        int studentId,
        string? search,
        PaginationParams pagination,
        CancellationToken cancellationToken = default)
    {
        var query = ResultsQuery().Where(a => a.Assignment.StudentId == studentId);

        if (SearchText.ToLikePattern(search) is { } pattern)
        {
            query = query.Where(a => EF.Functions.ILike(EF.Functions.Unaccent(a.Assignment.Exam.Title), pattern));
        }

        return await OrderForDisplay(query).ToPagedResultAsync(pagination, cancellationToken);
    }

    public async Task<int> CountCompletedByStudentAsync(int studentId, CancellationToken cancellationToken = default) =>
        await DbSet.CountAsync(
            a => a.Assignment.StudentId == studentId
                 && (a.Status == AssignmentStatus.Submitted || a.Status == AssignmentStatus.Graded),
            cancellationToken);

    /// <summary>Làm tròn 1 chữ số theo §5. Chưa có bài nào được chấm thì trả 0, không phải NULL.</summary>
    public async Task<decimal> GetAverageScoreByStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var average = await DbSet
            .Where(a => a.Assignment.StudentId == studentId && a.Status == AssignmentStatus.Graded)
            .AverageAsync(a => (decimal?)a.Score, cancellationToken);

        return average.HasValue ? Round1(average.Value) : 0m;
    }

    /// <summary>
    /// Hai con số dẫn xuất cho nhiều học sinh trong một truy vấn. Dùng <c>SUM</c> trên biểu thức
    /// điều kiện thay vì <c>Count(predicate)</c>/<c>Average</c> lồng trong nhóm: cả ba đều ra cùng
    /// kết quả nhưng chỉ dạng này chắc chắn dịch được thành một câu <c>GROUP BY</c> duy nhất.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, (int Completed, decimal AverageScore)>> GetStatsByStudentsAsync(
        IEnumerable<int> studentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = studentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, (int, decimal)>();
        }

        var rows = await DbSet
            .AsNoTracking()
            .Where(a => ids.Contains(a.Assignment.StudentId))
            .GroupBy(a => a.Assignment.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Completed = g.Sum(a =>
                    a.Status == AssignmentStatus.Submitted || a.Status == AssignmentStatus.Graded ? 1 : 0),
                GradedCount = g.Sum(a => a.Status == AssignmentStatus.Graded ? 1 : 0),
                ScoreSum = g.Sum(a => a.Status == AssignmentStatus.Graded ? a.Score : 0m)
            })
            .ToListAsync(cancellationToken);

        var statsByStudent = rows.ToDictionary(
            row => row.StudentId,
            row => (
                Completed: row.Completed,
                AverageScore: row.GradedCount == 0 ? 0m : Round1(row.ScoreSum / row.GradedCount)));

        // Học sinh chưa làm bài nào vẫn phải có mặt với (0, 0).
        return ids.ToDictionary(id => id, id => statsByStudent.GetValueOrDefault(id, (0, 0m)));
    }

    public async Task<int> CountSubmittedSinceAsync(
        int teacherId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default) =>
        await DbSet.CountAsync(
            a => a.Assignment.Exam.CreatedByUserId == teacherId
                 && a.SubmittedAt != null
                 && a.SubmittedAt >= sinceUtc,
            cancellationToken);

    private IQueryable<ExamAttempt> ResultsQuery() =>
        DbSet
            .AsNoTracking()
            .Include(a => a.Assignment)
                .ThenInclude(assignment => assignment.Exam)
            .Include(a => a.Assignment)
                .ThenInclude(assignment => assignment.Student)
                    .ThenInclude(student => student.Group);

    /// <summary>
    /// Mới nhất trước, sắp theo <c>StartedAt</c> chứ không theo <c>SubmittedAt</c>: cột nộp bài
    /// còn NULL với bài đang làm dở, mà PostgreSQL xếp NULL lên đầu khi sắp giảm dần — bảng kết quả
    /// sẽ mở ra toàn dòng chưa có điểm.
    /// </summary>
    private static IQueryable<ExamAttempt> OrderForDisplay(IQueryable<ExamAttempt> query) =>
        query
            .OrderByDescending(a => a.StartedAt)
            .ThenByDescending(a => a.Id);

    private static decimal Round1(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
