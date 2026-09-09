using EduQuiz.Application.Interfaces.Repositories;
using EduQuiz.Application.Interfaces.UnitOfWork;
using EduQuiz.Infrastructure.Persistence;
using EduQuiz.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EduQuiz.Infrastructure.UnitOfWork;

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

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

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
