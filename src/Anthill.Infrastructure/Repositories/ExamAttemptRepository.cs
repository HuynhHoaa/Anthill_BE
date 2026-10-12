using Anthill.Application.Common;
using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

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

                    // Nhóm lớp: bản tóm tắt kết quả có cột nhóm, mà không nạp thì EF trả về null
                    // lặng lẽ — dự án không bật lazy loading proxy — và cột đó luôn rỗng.
                    .ThenInclude(student => student.Group)
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
        //
        // Bộ lọc đi qua đúng một hàm dùng chung với phép đếm số liệu. Giữ hai bản sao thì bảng và
        // bốn ô phía trên nó sẽ lệch nhau ngay lần đầu có ai sửa một bên mà quên bên kia — mà đó
        // là hai phần nằm cạnh nhau trên cùng một màn hình.
        var query = ApplyResultFilters(
            ResultsQuery().Where(a => a.Assignment.Exam.CreatedByUserId == teacherId),
            search, examId, studentId, groupId, status);

        return await OrderForDisplay(query).ToPagedResultAsync(pagination, cancellationToken);
    }

    public async Task<PagedResult<ExamAttempt>> GetMyResultsPagedAsync(
        int studentId,
        string? search,
        PaginationParams pagination,
        CancellationToken cancellationToken = default)
    {
        // Chỉ lượt đã nộp hoặc đã chấm. Lượt InProgress mà lọt vào đây thì cả trang "Kết quả của tôi"
        // lẫn mục kết quả gần đây ở trang tổng quan đếm nó như một bài đã nộp, trong khi em vẫn đang
        // làm và đồng hồ còn chạy.
        var query = ResultsQuery().Where(a =>
            a.Assignment.StudentId == studentId
            && (a.Status == AssignmentStatus.Submitted || a.Status == AssignmentStatus.Graded));

        if (SearchText.ToLikePattern(search) is { } pattern)
        {
            query = query.Where(a =>
                EF.Functions.ILike(EF.Functions.Unaccent(a.Assignment.Exam.Title), pattern)
                || EF.Functions.ILike(EF.Functions.Unaccent(a.Assignment.Exam.Topic ?? string.Empty), pattern));
        }

        return await OrderForDisplay(query).ToPagedResultAsync(pagination, cancellationToken);
    }

    public async Task<int> CountCompletedByStudentAsync(int studentId, CancellationToken cancellationToken = default) =>
        await DbSet.CountAsync(
            a => a.Assignment.StudentId == studentId
                 && (a.Status == AssignmentStatus.Submitted || a.Status == AssignmentStatus.Graded),
            cancellationToken);

    /// <summary>
    /// Làm tròn 1 chữ số theo §5. Chưa có bài nào được chấm thì trả 0, không phải NULL.
    /// </summary>
    /// <remarks>
    /// Quy mỗi lượt về thang 10 (<c>Score / MaxScore * 10</c>) TRƯỚC khi lấy trung bình (H7): một
    /// học sinh thường làm nhiều đề với điểm tối đa khác nhau (đề 10 câu, đề 20 câu…), và gộp thẳng
    /// điểm thô của chúng lại là cộng táo với cam — điểm trung bình ra sai thang, không nói lên
    /// được gì. Loại `MaxScore <= 0` để tránh chia cho 0 (không xảy ra với dữ liệu hợp lệ, nhưng
    /// truy vấn không được phép ném lỗi chỉ vì một dòng dữ liệu bất thường).
    /// </remarks>
    public async Task<decimal> GetAverageScoreByStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var average = await DbSet
            .Where(a => a.Assignment.StudentId == studentId && a.Status == AssignmentStatus.Graded && a.MaxScore > 0)
            .AverageAsync(a => (decimal?)(a.Score / a.MaxScore * 10m), cancellationToken);

        return average.HasValue ? Round1(average.Value) : 0m;
    }

    /// <summary>
    /// Hai con số dẫn xuất cho nhiều học sinh trong một truy vấn. Dùng <c>SUM</c> trên biểu thức
    /// điều kiện thay vì <c>Count(predicate)</c>/<c>Average</c> lồng trong nhóm: cả ba đều ra cùng
    /// kết quả nhưng chỉ dạng này chắc chắn dịch được thành một câu <c>GROUP BY</c> duy nhất.
    /// </summary>
    /// <remarks>
    /// Cũng quy mỗi lượt về thang 10 trước khi cộng dồn, cùng lý do với
    /// <see cref="GetAverageScoreByStudentAsync"/> (H7) — một học sinh làm nhiều đề khác điểm tối
    /// đa thì tổng điểm thô không chia đều ra một trung bình có nghĩa.
    /// </remarks>
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
                GradedCount = g.Sum(a => a.Status == AssignmentStatus.Graded && a.MaxScore > 0 ? 1 : 0),
                NormalizedScoreSum = g.Sum(a =>
                    a.Status == AssignmentStatus.Graded && a.MaxScore > 0 ? a.Score / a.MaxScore * 10m : 0m)
            })
            .ToListAsync(cancellationToken);

        var statsByStudent = rows.ToDictionary(
            row => row.StudentId,
            row => (
                Completed: row.Completed,
                AverageScore: row.GradedCount == 0 ? 0m : Round1(row.NormalizedScoreSum / row.GradedCount)));

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

    public async Task<ResultStatsSnapshot> GetResultStatsAsync(
        int teacherId,
        string? search,
        int? examId,
        int? studentId,
        int? groupId,
        AssignmentStatus? status,
        CancellationToken cancellationToken = default)
    {
        // Cùng bộ lọc với bảng, nhưng không Include gì: đây là phép đếm, không phải phép đọc.
        var attempts = ApplyResultFilters(
            DbSet.AsNoTracking().Where(a => a.Assignment.Exam.CreatedByUserId == teacherId),
            search, examId, studentId, groupId, status);

        // Một lượt đi database cho cả năm con số. GroupBy trên hằng số là cách gom nhiều phép tổng
        // hợp vào một câu lệnh SELECT mà EF Core dịch được.
        var aggregate = await attempts
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Graded = g.Count(a => a.Status == AssignmentStatus.Graded),
                Submitted = g.Count(a => a.Status == AssignmentStatus.Submitted || a.Status == AssignmentStatus.Graded),
                InProgress = g.Count(a => a.Status == AssignmentStatus.InProgress),

                // Lọc ngay trong phép tổng hợp: gộp cả bài đang làm dở vào mẫu số sẽ kéo trung bình
                // xuống theo tiến độ chấm chứ không theo năng lực học sinh.
                //
                // Quy về thang 10 (Score / MaxScore * 10) trước khi lấy Average/Max (H7): bộ lọc ở
                // đây thường trải trên nhiều đề khác nhau của cùng một giáo viên/lớp, mỗi đề một
                // MaxScore riêng — gộp điểm thô của chúng cho ra một con số không có đơn vị chung.
                // Loại MaxScore <= 0 để không chia cho 0 (không có dữ liệu hợp lệ nào rơi vào đây).
                Average = g.Where(a => a.Status == AssignmentStatus.Graded && a.MaxScore > 0)
                    .Average(a => (decimal?)(a.Score / a.MaxScore * 10m)),
                Highest = g.Where(a => a.Status == AssignmentStatus.Graded && a.MaxScore > 0)
                    .Max(a => (decimal?)(a.Score / a.MaxScore * 10m))
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Mẫu số của tỷ lệ hoàn thành đếm trên assignment, không trên attempt: em chưa từng vào thi
        // không có dòng attempt nào, mà chính các em đó mới là phần còn thiếu của tỷ lệ.
        //
        // Nhưng nó phải chịu ĐÚNG bộ lọc mà tử số đang chịu. Bỏ qua ô tìm kiếm ở đây thì gõ tên
        // một học sinh vào ra tỷ lệ 3,6% — ba bài đã nộp chia cho toàn bộ số bài đã giao của mọi
        // lớp. Riêng bộ lọc trạng thái thì cố ý không áp: lọc "đã chấm" rồi lấy mẫu số cũng chỉ
        // gồm bài đã chấm thì tỷ lệ luôn ra 100%, không nói lên điều gì.
        var assignedQuery = Context.Set<ExamAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.Exam.CreatedByUserId == teacherId);

        if (examId.HasValue) assignedQuery = assignedQuery.Where(a => a.ExamId == examId.Value);
        if (studentId.HasValue) assignedQuery = assignedQuery.Where(a => a.StudentId == studentId.Value);
        if (groupId.HasValue) assignedQuery = assignedQuery.Where(a => a.Student.GroupId == groupId.Value);

        if (SearchText.ToLikePattern(search) is { } assignedPattern)
        {
            assignedQuery = assignedQuery.Where(a =>
                EF.Functions.ILike(EF.Functions.Unaccent(a.Student.FullName), assignedPattern)
                || EF.Functions.ILike(a.Student.Code ?? string.Empty, assignedPattern)
                || EF.Functions.ILike(EF.Functions.Unaccent(a.Exam.Title), assignedPattern));
        }

        var assignedCount = await assignedQuery.CountAsync(cancellationToken);

        return new ResultStatsSnapshot
        {
            TotalAttempts = aggregate?.Total ?? 0,
            GradedCount = aggregate?.Graded ?? 0,
            SubmittedCount = aggregate?.Submitted ?? 0,
            InProgressCount = aggregate?.InProgress ?? 0,
            AssignedCount = assignedCount,
            AverageScore = Round1(aggregate?.Average ?? 0m),
            // Làm tròn giống AverageScore. Không làm thì hai ô cạnh nhau hiện "3,1" và "9,00" —
            // cùng một thang điểm mà hai định dạng, đọc vào tưởng hai đơn vị khác nhau.
            HighestScore = Round1(aggregate?.Highest ?? 0m)
        };
    }

    public async Task<IReadOnlyDictionary<int, int>> GetPaperQuestionCountsAsync(
        IEnumerable<int> attemptIds,
        CancellationToken cancellationToken = default)
    {
        var ids = attemptIds?.Distinct().ToList() ?? [];
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        // Bản đề riêng của học sinh nếu có, ngược lại là bản chung của cả lớp — cùng quy tắc mà
        // ExamGeneratorService dùng khi sinh đề, chỉ khác là ở đây chỉ cần đếm.
        var rows = await DbSet
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Select(a => new
            {
                AttemptId = a.Id,
                Count = Context.Set<ExamPaper>()
                    .Where(paper => paper.AssignmentId == a.AssignmentId
                                    || (paper.ExamId == a.Assignment.ExamId && paper.AssignmentId == null))

                    // Bản riêng thắng bản chung khi cả hai cùng tồn tại.
                    .OrderBy(paper => paper.AssignmentId == null ? 1 : 0)
                    .Select(paper => paper.Questions.Count)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.AttemptId, row => row.Count);
    }

    /// <summary>Bộ lọc dùng chung giữa bảng kết quả và phép đếm số liệu, để hai bên không lệch nhau.</summary>
    private static IQueryable<ExamAttempt> ApplyResultFilters(
        IQueryable<ExamAttempt> query,
        string? search,
        int? examId,
        int? studentId,
        int? groupId,
        AssignmentStatus? status)
    {
        if (examId.HasValue) query = query.Where(a => a.Assignment.ExamId == examId.Value);
        if (studentId.HasValue) query = query.Where(a => a.Assignment.StudentId == studentId.Value);
        if (groupId.HasValue) query = query.Where(a => a.Assignment.Student.GroupId == groupId.Value);
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);

        if (SearchText.ToLikePattern(search) is { } pattern)
        {
            query = query.Where(a =>
                EF.Functions.ILike(EF.Functions.Unaccent(a.Assignment.Student.FullName), pattern)
                || EF.Functions.ILike(a.Assignment.Student.Code ?? string.Empty, pattern)
                || EF.Functions.ILike(EF.Functions.Unaccent(a.Assignment.Exam.Title), pattern));
        }

        return query;
    }

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
