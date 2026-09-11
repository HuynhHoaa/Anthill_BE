using Anthill.Application.Interfaces.Repositories;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using Anthill.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anthill.Infrastructure.Repositories;

public class ExamAssignmentRepository : GenericRepository<ExamAssignment>, IExamAssignmentRepository
{
    public ExamAssignmentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ExamAssignment>> GetByExamAsync(
        int examId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            // Mọi dòng cùng một đề: giữ một thể hiện Exam chung thay vì chép ra N bản.
            .AsNoTrackingWithIdentityResolution()
            .Include(a => a.Exam)
            .Include(a => a.Student)
                .ThenInclude(s => s.Group)
            .Include(a => a.Attempt)
            .Where(a => a.ExamId == examId)
            .OrderBy(a => a.Student.Code)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

    public async Task<ExamAssignment?> GetByExamAndStudentAsync(
        int examId,
        int studentId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(a => a.Exam)
            .Include(a => a.Attempt)
            .FirstOrDefaultAsync(a => a.ExamId == examId && a.StudentId == studentId, cancellationToken);

    public async Task<IReadOnlyList<ExamAssignment>> GetByExamViaGroupsAsync(
        int examId,
        IEnumerable<int> groupIds,
        CancellationToken cancellationToken = default)
    {
        var ids = groupIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        // Có theo dõi thay đổi: service sửa OpenAt/CloseAt ngay trên các bản ghi này rồi SaveChanges.
        return await DbSet
            .Include(a => a.Attempt)
            .Where(a => a.ExamId == examId
                        && a.AssignedViaGroupId != null
                        && ids.Contains(a.AssignedViaGroupId.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(DateTime OpenAt, DateTime CloseAt)>> GetPendingOverrideWindowsAsync(
        int examId,
        CancellationToken cancellationToken = default)
    {
        var rows = await DbSet
            .AsNoTracking()
            .Where(a => a.ExamId == examId && a.OpenAt != null && a.CloseAt != null && a.Attempt == null)
            .Select(a => new { OpenAt = a.OpenAt!.Value, CloseAt = a.CloseAt!.Value })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows.Select(row => (row.OpenAt, row.CloseAt)).ToList();
    }

    /// <remarks>
    /// <para>
    /// Vế đầu là vế CloseAt của <c>AssignmentSchedule.Resolve</c> viết lại bằng SQL: lượt có đủ cặp giờ
    /// riêng lấy giờ riêng, còn lại lấy giờ của đề. Tính MAX ngay trong database vì một đề giao cho năm
    /// trăm em thì nạp năm trăm dòng về chỉ để lấy một con số. Ép kiểu nullable để tập rỗng trả NULL
    /// thay vì ném lỗi.
    /// </para>
    /// <para>
    /// Vế thứ hai: hạn nộp của lượt đang làm đã chốt lúc bắt đầu. Giáo viên kéo giờ đóng sớm lại sau
    /// đó thì một em vẫn đang làm có thể còn giờ quá mốc mới — mở đáp án theo mốc mới là mở ngay trước
    /// mặt em ấy.
    /// </para>
    /// </remarks>
    public async Task<DateTime?> GetAnswersReleaseAtAsync(int examId, CancellationToken cancellationToken = default)
    {
        var latestClose = await DbSet
            .AsNoTracking()
            .Where(a => a.ExamId == examId)
            .Select(a => (DateTime?)(a.OpenAt != null && a.CloseAt != null ? a.CloseAt.Value : a.Exam.CloseAt))
            .MaxAsync(cancellationToken);

        var latestRunning = await Context.ExamAttempts
            .AsNoTracking()
            .Where(attempt => attempt.Assignment.ExamId == examId && attempt.Status == AssignmentStatus.InProgress)
            .Select(attempt => (DateTime?)attempt.ExpiresAt)
            .MaxAsync(cancellationToken);

        return latestRunning is { } running && (latestClose is null || running > latestClose)
            ? running
            : latestClose;
    }

    /// <summary>Nạp kèm Exam, Student, Attempt và bản đề riêng — mọi kiểm tra quyền và hạn giờ đều cần.</summary>
    public async Task<ExamAssignment?> GetDetailAsync(
        int assignmentId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(a => a.Exam)
                .ThenInclude(e => e.CreatedBy)
            .Include(a => a.Student)
            .Include(a => a.Attempt)
            .Include(a => a.Paper)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);

    public async Task<IReadOnlyList<int>> GetAssignedStudentIdsAsync(
        int examId,
        IEnumerable<int> studentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = studentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await DbSet
            .AsNoTracking()
            .Where(a => a.ExamId == examId && ids.Contains(a.StudentId))
            .Select(a => a.StudentId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExamAssignment>> GetForStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Include(a => a.Exam)
                .ThenInclude(e => e.CreatedBy)
            .Include(a => a.Attempt)
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

    public async Task<bool> IsOwnedByStudentAsync(
        int assignmentId,
        int studentId,
        CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(a => a.Id == assignmentId && a.StudentId == studentId, cancellationToken);

    public async Task<int> CountByExamAsync(int examId, CancellationToken cancellationToken = default) =>
        await DbSet.CountAsync(a => a.ExamId == examId, cancellationToken);

    public async Task<int> CountSubmittedByExamAsync(int examId, CancellationToken cancellationToken = default) =>
        await Context.ExamAttempts.CountAsync(
            attempt => attempt.Assignment.ExamId == examId
                       && (attempt.Status == AssignmentStatus.Submitted || attempt.Status == AssignmentStatus.Graded),
            cancellationToken);

    public async Task<int> CountStartedByExamAsync(int examId, CancellationToken cancellationToken = default) =>
        await Context.ExamAttempts.CountAsync(attempt => attempt.Assignment.ExamId == examId, cancellationToken);

    /// <remarks>
    /// Sắp theo <c>Id</c> để hai lần khoá cùng một đề luôn giành dòng theo cùng một thứ tự — khoá theo
    /// thứ tự tuỳ ý là cách kinh điển để hai giao dịch chờ nhau mãi. Không nối thêm toán tử LINQ nào
    /// phía sau, để EF gửi thẳng câu lệnh xuống thay vì bọc nó vào một truy vấn con.
    /// </remarks>
    public async Task<IReadOnlyList<ExamAssignment>> LockByExamAsync(
        int examId,
        CancellationToken cancellationToken = default) =>
        await DbSet
            .FromSql($"""SELECT * FROM "ExamAssignments" WHERE "ExamId" = {examId} ORDER BY "Id" FOR UPDATE""")
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<ExamAssignment?> LockForStartAsync(
        int assignmentId,
        CancellationToken cancellationToken = default)
    {
        var trackedBefore = Context.ChangeTracker
            .Entries<ExamAssignment>()
            .Any(entry => entry.Entity.Id == assignmentId);

        var rows = await DbSet
            .FromSql($"""SELECT * FROM "ExamAssignments" WHERE "Id" = {assignmentId} FOR SHARE""")
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var assignment = rows[0];

        // Mọi lần đọc dưới đây là câu lệnh mới sau khi đã cầm khoá, nên ở mức READ COMMITTED chúng thấy
        // mọi thứ lần sửa đề vừa commit. Chỉ thực thể đã theo dõi từ trước là cần đọc lại tường minh.
        if (trackedBefore)
        {
            // Khung giờ riêng của lượt giao có thể vừa được sửa bằng một lần giao lại.
            await Context.Entry(assignment).ReloadAsync(cancellationToken);
        }

        var examEntry = Context.Entry(assignment).Reference(a => a.Exam);
        if (assignment.Exam is not null)
        {
            await Context.Entry(assignment.Exam).ReloadAsync(cancellationToken);
        }
        else
        {
            await examEntry.LoadAsync(cancellationToken);
        }

        if (assignment.Attempt is not null)
        {
            await Context.Entry(assignment.Attempt).ReloadAsync(cancellationToken);
        }
        else
        {
            // Tab thứ hai của cùng em có thể vừa tạo lượt trong lúc lượt này chờ khoá.
            await Context.Entry(assignment).Reference(a => a.Attempt).LoadAsync(cancellationToken);
        }

        if (assignment.Paper is not null)
        {
            // Bản đề đã bị xoá thì ReloadAsync gỡ nó khỏi change tracker thay vì ném lỗi.
            await Context.Entry(assignment.Paper).ReloadAsync(cancellationToken);
        }

        return assignment;
    }

    /// <summary>
    /// Ba con số dẫn xuất cho nhiều đề trong ba truy vấn cố định, thay cho 3×N lời gọi ở màn hình
    /// danh sách đề. Cố ý tách làm ba lượt <c>GROUP BY</c> đơn giản thay vì một truy vấn có
    /// <c>COUNT</c> điều kiện lồng qua navigation — ba lượt này chắc chắn dịch được sang SQL.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, (int Assigned, int Submitted, int Started)>> CountByExamsAsync(
        IEnumerable<int> examIds,
        CancellationToken cancellationToken = default)
    {
        var ids = examIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, (int, int, int)>();
        }

        var assigned = await DbSet
            .AsNoTracking()
            .Where(a => ids.Contains(a.ExamId))
            .GroupBy(a => a.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var submitted = await Context.ExamAttempts
            .AsNoTracking()
            .Where(attempt => ids.Contains(attempt.Assignment.ExamId)
                              && (attempt.Status == AssignmentStatus.Submitted || attempt.Status == AssignmentStatus.Graded))
            .GroupBy(attempt => attempt.Assignment.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var started = await Context.ExamAttempts
            .AsNoTracking()
            .Where(attempt => ids.Contains(attempt.Assignment.ExamId))
            .GroupBy(attempt => attempt.Assignment.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var submittedByExam = submitted.ToDictionary(row => row.ExamId, row => row.Count);
        var assignedByExam = assigned.ToDictionary(row => row.ExamId, row => row.Count);
        var startedByExam = started.ToDictionary(row => row.ExamId, row => row.Count);

        // Đề chưa giao cho ai vẫn phải có mặt với (0, 0, 0) — thiếu khoá là màn hình hiện ô trống.
        return ids.ToDictionary(
            id => id,
            id => (
                Assigned: assignedByExam.GetValueOrDefault(id),
                Submitted: submittedByExam.GetValueOrDefault(id),
                Started: startedByExam.GetValueOrDefault(id)));
    }

    public async Task RemoveByExamAsync(int examId, CancellationToken cancellationToken = default)
    {
        var assignments = await DbSet
            .Where(a => a.ExamId == examId)
            .ToListAsync(cancellationToken);

        // Attempt, StudentAnswer và bản đề riêng đi theo nhờ ON DELETE CASCADE ở database.
        DbSet.RemoveRange(assignments);
    }
}
