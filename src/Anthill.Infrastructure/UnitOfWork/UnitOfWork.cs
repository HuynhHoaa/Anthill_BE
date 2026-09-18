using Anthill.Application.Common;
using Anthill.Application.Interfaces.Repositories;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Infrastructure.Persistence;
using Anthill.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Anthill.Infrastructure.UnitOfWork;

/// <summary>
/// Gom chín repository quanh <b>một</b> <see cref="AppDbContext"/> và một lần ghi duy nhất.
/// </summary>
/// <remarks>
/// Repository khởi tạo lười (<c>??=</c>): một request chỉ chạm hai ba bảng, không cần dựng cả chín
/// đối tượng cho mỗi lần gọi. Tất cả dùng chung một DbContext nên thay đổi trên nhiều bảng nằm
/// trong cùng một <see cref="SaveChangesAsync"/>.
/// </remarks>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    private IUserRepository? _users;
    private ISubjectRepository? _subjects;
    private IStudentGroupRepository? _studentGroups;
    private IQuestionRepository? _questions;
    private IExamRepository? _exams;
    private IExamAssignmentRepository? _examAssignments;
    private IExamAttemptRepository? _examAttempts;
    private IStudentAnswerRepository? _studentAnswers;
    private INotificationRepository? _notifications;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IUserRepository Users => _users ??= new UserRepository(_context);

    public ISubjectRepository Subjects => _subjects ??= new SubjectRepository(_context);

    public IStudentGroupRepository StudentGroups => _studentGroups ??= new StudentGroupRepository(_context);

    public IQuestionRepository Questions => _questions ??= new QuestionRepository(_context);

    public IExamRepository Exams => _exams ??= new ExamRepository(_context);

    public IExamAssignmentRepository ExamAssignments => _examAssignments ??= new ExamAssignmentRepository(_context);

    public IExamAttemptRepository ExamAttempts => _examAttempts ??= new ExamAttemptRepository(_context);

    public IStudentAnswerRepository StudentAnswers => _studentAnswers ??= new StudentAnswerRepository(_context);

    public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);

    /// <remarks>
    /// Bọc riêng vi phạm ràng buộc duy nhất (SQLSTATE <c>23505</c>) thành
    /// <see cref="UniqueConstraintViolationException"/>: tầng Application không tham chiếu EF Core hay
    /// Npgsql, nên không tự bắt được <see cref="DbUpdateException"/>/<c>PostgresException</c>. Nơi gọi
    /// không bắt riêng thì <c>ExceptionMiddleware</c> vẫn dịch ngoại lệ mới này thành 409, giống hệt khi
    /// nó còn là <see cref="DbUpdateException"/> thô — xem <see cref="UniqueConstraintViolationException"/>.
    /// </remarks>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new UniqueConstraintViolationException(
                "Một ràng buộc duy nhất của database vừa bị vi phạm — hai request cùng ghi một tài nguyên.",
                exception);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>
    /// Mở transaction cho ba nghiệp vụ bắt buộc phải nguyên tử: giao bài, nộp bài và xoá đề.
    /// </summary>
    /// <remarks>
    /// Cả ba đều xen kẽ đọc và ghi, nên chỉ <see cref="SaveChangesAsync"/> là không đủ. Cũng vì
    /// chúng mà cấu hình Npgsql cố ý <b>không</b> bật <c>EnableRetryOnFailure</c>: execution
    /// strategy có thử lại sẽ từ chối transaction do người dùng tự mở.
    /// </remarks>
    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return new EfTransactionScope(transaction);
    }
}
