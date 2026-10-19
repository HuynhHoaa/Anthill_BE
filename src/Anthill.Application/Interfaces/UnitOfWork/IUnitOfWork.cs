using Anthill.Application.Interfaces.Repositories;

namespace Anthill.Application.Interfaces.UnitOfWork;

/// <summary>
/// Gom mọi repository quanh một phiên làm việc với database và một lần ghi duy nhất.
/// </summary>
/// <remarks>
/// <see cref="BeginTransactionAsync"/> tồn tại vì §5 có ba nghiệp vụ bắt buộc phải nguyên tử:
/// <list type="bullet">
/// <item><b>Giao bài</b> — tạo assignment + sinh ExamPaper + tạo Notification.</item>
/// <item><b>Nộp bài</b> — ghi StudentAnswer + chấm điểm + cập nhật attempt + báo giáo viên.</item>
/// <item><b>Xoá đề</b> — xoá assignment, xoá bản đề đã sinh rồi mới xoá đề.</item>
/// </list>
/// Chỉ <see cref="SaveChangesAsync"/> là không đủ cho ba việc này vì chúng còn kèm truy vấn
/// đọc ở giữa; thiếu transaction thì một lỗi giữa chừng để lại dữ liệu nửa vời.
/// </remarks>
public interface IUnitOfWork
{
    IUserRepository Users { get; }

    ISubjectRepository Subjects { get; }

    IAcademicTermRepository AcademicTerms { get; }

    IStudentGroupRepository StudentGroups { get; }

    IQuestionRepository Questions { get; }

    IExamRepository Exams { get; }

    IExamAssignmentRepository ExamAssignments { get; }

    IExamAttemptRepository ExamAttempts { get; }

    IStudentAnswerRepository StudentAnswers { get; }

    INotificationRepository Notifications { get; }

    IAccountChangeRepository AccountChanges { get; }

    IGroupChangeRepository GroupChanges { get; }

    /// <summary>Số bản ghi đã ghi xuống database.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Mở transaction. Không commit trước khi rời khối <c>using</c> nghĩa là rollback —
    /// đó là hành vi mặc định mong muốn khi có exception.
    /// </summary>
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Transaction đang mở. Đặt ở tầng Application để service không phải biết EF Core;
/// phần cài đặt bọc <c>IDbContextTransaction</c> ở tầng Infrastructure.
/// </summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
