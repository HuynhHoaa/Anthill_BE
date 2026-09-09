using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.ExamAssignments;
using EduQuiz.Application.Interfaces.Security;
using EduQuiz.Application.Interfaces.Services;
using EduQuiz.Application.Interfaces.UnitOfWork;
using EduQuiz.Application.Mappings;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Services;

/// <summary>
/// Cài đặt <see cref="IExamAssignmentService"/> — ba endpoint giao bài của §4.6.
/// </summary>
/// <remarks>
/// Bốn điều quyết định nằm ở file này:
/// <list type="number">
/// <item><b>Giao bài là thao tác theo lô, và một lô hỏng một phần thì phần còn lại vẫn phải chạy.</b>
/// Giáo viên chọn cả lớp bốn mươi em; một em có tài khoản khoá không được phép làm hỏng ba mươi
/// chín em kia. Đổi lại, những gì bị bỏ qua phải được <b>đếm và trả về</b>, vì im lặng ở đây
/// nghĩa là tới hôm thi mới phát hiện thiếu bài.</item>
/// <item><b>Idempotent theo cặp (đề, học sinh).</b> Bấm giao hai lần không tạo ra hai bài, và
/// quan trọng hơn: không xoá mất lượt làm bài hay điểm của em đã làm rồi.</item>
/// <item><b>Phạm vi tính theo nhóm lớp mà giáo viên quản lý.</b> Không có hàng rào này thì một
/// giáo viên đoán id là giao được bài cho lớp của đồng nghiệp.</item>
/// <item><b>Sinh đề nằm trong cùng transaction với tạo assignment.</b> Tách ra thì có lúc tồn tại
/// một assignment không có đề — học sinh mở bài ra và thấy một trang trắng.</item>
/// </list>
/// </remarks>
public sealed class ExamAssignmentService : IExamAssignmentService
{
    private const string ExamNotFoundMessage = "Không tìm thấy bài kiểm tra.";
    private const string AssignmentNotFoundMessage = "Không tìm thấy bản ghi giao bài này.";

    private const string AdminCannotAssignMessage =
        "Quản trị viên chỉ xem được bài kiểm tra; việc giao bài thuộc về giáo viên phụ trách môn.";

    private const string DraftCannotAssignMessage =
        "Bài kiểm tra này còn là bản nháp nên chưa giao được. Hãy xuất bản trước.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IExamGeneratorService _generator;

    public ExamAssignmentService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IExamGeneratorService generator)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _generator = generator;
    }

    private int CurrentUserId => _currentUser.RequireUserId();

    private bool IsAdmin => _currentUser.IsInRole(UserRole.Admin);

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ExamAssignmentDto>>> ListAsync(
        int examId,
        CancellationToken cancellationToken = default)
    {
        var exam = await FindReadableExamAsync(examId, cancellationToken);
        if (exam.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ExamAssignmentDto>>(exam.Error!, exam.ErrorCode);
        }

        var asOf = DateTime.UtcNow;
        var assignments = await _unitOfWork.ExamAssignments.GetByExamAsync(examId, cancellationToken);

        // Gắn đề vào từng bản ghi để ResolveStatus biết đề đã đóng chưa. Repository không nạp sẵn
        // Exam ở truy vấn này, mà cả danh sách vốn thuộc đúng một đề nên gán thẳng là đủ.
        foreach (var assignment in assignments)
        {
            assignment.Exam = exam.ValueOrThrow;
        }

        IReadOnlyList<ExamAssignmentDto> result =
            assignments.Select(assignment => assignment.ToDto(asOf)).ToList();

        return Result.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result<AssignExamResponse>> AssignAsync(
        int examId,
        AssignExamRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var found = await FindOwnedExamAsync(examId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<AssignExamResponse>(found.Error!, found.ErrorCode);
        }

        var exam = found.ValueOrThrow;

        // Frontend không kiểm điều này. Giao một bản nháp nghĩa là phát cho học sinh một đề mà
        // chính giáo viên còn chưa chốt tỷ lệ, và blueprint của nó có thể chưa sinh nổi câu hỏi.
        if (exam.IsDraft)
        {
            return Result.Conflict<AssignExamResponse>(DraftCannotAssignMessage);
        }

        var targets = await ResolveTargetsAsync(request, cancellationToken);

        var alreadyAssigned = (await _unitOfWork.ExamAssignments.GetAssignedStudentIdsAsync(
            examId,
            targets.InScope.Select(student => student.Id),
            cancellationToken)).ToHashSet();

        var newcomers = targets.InScope
            .Where(student => !alreadyAssigned.Contains(student.Id))
            .ToList();

        var response = new AssignExamResponse
        {
            AssignedCount = newcomers.Count,
            SkippedAlreadyAssigned = alreadyAssigned.Count,
            SkippedLocked = targets.LockedCount,
            SkippedNotFound = targets.NotFoundCount,
            SkippedOutOfScope = targets.OutOfScopeCount
        };

        if (newcomers.Count == 0)
        {
            // Không có gì để ghi. Vẫn trả về 200 kèm các con số: người dùng cần biết vì sao không
            // ai được giao thêm, và "đã giao hết rồi" là một câu trả lời hợp lệ chứ không phải lỗi.
            return Result.Success(response);
        }

        var now = DateTime.UtcNow;

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        // Cờ này thuộc về đề chứ không thuộc về từng assignment: một đề mà nửa lớp dùng chung đề
        // còn nửa kia mỗi em một đề thì kết quả không so sánh với nhau được nữa.
        if (exam.RandomizePerStudent != request.RandomizePerStudent)
        {
            exam.RandomizePerStudent = request.RandomizePerStudent;
            exam.UpdatedAt = now;
            _unitOfWork.Exams.Update(exam);
        }

        var assignments = newcomers
            .Select(student => new ExamAssignment
            {
                ExamId = exam.Id,
                StudentId = student.Id,
                AssignedAt = now,
                CreatedAt = now
            })
            .ToList();

        await _unitOfWork.ExamAssignments.AddRangeAsync(assignments, cancellationToken);

        // Cần id thật của assignment trước khi sinh đề: hạt giống của bản đề riêng dựng từ id đó.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var generated = await GeneratePapersAsync(exam, assignments, cancellationToken);
        if (generated.IsFailure)
        {
            // Không sinh nổi đề thì không giao. Cuộn lại cả lô thay vì để lại những assignment
            // trỏ vào một bản đề không tồn tại.
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<AssignExamResponse>(generated.Error!, generated.ErrorCode);
        }

        if (request.Notify)
        {
            await AddNotificationsAsync(exam, assignments, now, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(response);
    }

    /// <inheritdoc />
    public async Task<Result> RevokeAsync(
        int examId,
        int assignmentId,
        CancellationToken cancellationToken = default)
    {
        var found = await FindOwnedExamAsync(examId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure(found.Error!, found.ErrorCode);
        }

        var assignment = await _unitOfWork.ExamAssignments.GetDetailAsync(assignmentId, cancellationToken);

        // Kiểm cả assignmentId lẫn examId khớp nhau: thiếu vế thứ hai thì đổi số trên thanh địa chỉ
        // là thu hồi được bài của một đề khác.
        if (assignment is null || assignment.ExamId != examId)
        {
            return Result.NotFound(AssignmentNotFoundMessage);
        }

        if (assignment.Attempt is not null)
        {
            return Result.Conflict(
                "Học sinh này đã bắt đầu làm bài nên không thu hồi được nữa. "
                + "Thu hồi lúc này là xoá luôn phần bài em đã làm.");
        }

        // Bản đề riêng của em này đi theo nhờ cascade khai ở ExamPaperConfiguration
        // (Assignment 1-1 Paper, DeleteBehavior.Cascade). Bản đề chung của cả lớp không gắn với
        // assignment nào nên không bị đụng tới — những em khác vẫn đang làm trên đó.
        //
        // Một lệnh xoá thì SaveChanges đã tự nguyên tử, không cần mở transaction riêng.
        _unitOfWork.ExamAssignments.Remove(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>Kết quả gom và sàng lọc danh sách học sinh được chọn.</summary>
    private sealed record Targets
    {
        /// <summary>Những em thật sự giao được: có thật, đúng vai trò, đang hoạt động, thuộc lớp của giáo viên.</summary>
        public required IReadOnlyList<User> InScope { get; init; }

        public required int NotFoundCount { get; init; }

        public required int LockedCount { get; init; }

        public required int OutOfScopeCount { get; init; }
    }

    /// <summary>
    /// Gom học sinh chọn lẻ với học sinh của các nhóm đã chọn, khử trùng lặp, rồi sàng ba lớp.
    /// </summary>
    /// <remarks>
    /// Thứ tự sàng có ý nghĩa: <b>không tồn tại</b> → <b>ngoài phạm vi</b> → <b>đang khoá</b>. Một
    /// em vừa ngoài phạm vi vừa bị khoá thì chỉ được đếm một lần, ở lý do đầu tiên chạm tới — nếu
    /// không, tổng các con số bỏ qua sẽ lớn hơn số em được chọn và bảng kết quả đọc vào vô nghĩa.
    /// </remarks>
    private async Task<Targets> ResolveTargetsAsync(
        AssignExamRequest request,
        CancellationToken cancellationToken)
    {
        // HashSet ngay từ đầu: màn hình giao bài cho chọn một em rồi chọn luôn cả nhóm chứa em đó,
        // nên trùng lặp là tình huống thường gặp chứ không phải ngoại lệ.
        var requestedIds = new HashSet<int>(request.StudentIds);

        // Nhóm của giáo viên khác bị loại ngay tại đây: repository chỉ trả về nhóm do người này sở
        // hữu, nên một id nhóm đoán bừa không kéo theo học sinh nào.
        var ownedGroups = await _unitOfWork.StudentGroups.GetOwnedByIdsAsync(
            request.GroupIds,
            CurrentUserId,
            cancellationToken);

        foreach (var group in ownedGroups)
        {
            var members = await _unitOfWork.Users.GetStudentsByGroupAsync(group.Id, cancellationToken);
            foreach (var member in members)
            {
                requestedIds.Add(member.Id);
            }
        }

        if (requestedIds.Count == 0)
        {
            return new Targets { InScope = [], NotFoundCount = 0, LockedCount = 0, OutOfScopeCount = 0 };
        }

        // Chỉ trả về id có thật và đúng vai trò Student. Chênh lệch số lượng chính là số id lạ.
        var students = await _unitOfWork.Users.GetStudentsByIdsAsync(requestedIds, cancellationToken);
        var notFound = requestedIds.Count - students.Count;

        // Phạm vi: em phải thuộc một nhóm do chính giáo viên này quản lý. Không có hàng rào này
        // thì đoán id là giao được bài cho lớp của đồng nghiệp.
        var myGroupIds = (await _unitOfWork.StudentGroups.GetOwnedAsync(CurrentUserId, search: null, cancellationToken))
            .Select(group => group.Id)
            .ToHashSet();

        var inScope = new List<User>(students.Count);
        var outOfScope = 0;
        var locked = 0;

        foreach (var student in students)
        {
            if (student.GroupId is null || !myGroupIds.Contains(student.GroupId.Value))
            {
                outOfScope++;
                continue;
            }

            // Tài khoản khoá thì không đăng nhập được, nên tạo bài cho em cũng chỉ tạo ra một dòng
            // không ai dùng tới. Frontend hiện vẫn cho chọn và vẫn tạo.
            if (student.Status != UserStatus.Active)
            {
                locked++;
                continue;
            }

            inScope.Add(student);
        }

        return new Targets
        {
            InScope = inScope,
            NotFoundCount = notFound,
            LockedCount = locked,
            OutOfScopeCount = outOfScope
        };
    }

    /// <summary>
    /// Sinh bản đề cho những assignment vừa tạo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sinh <b>ngay lúc giao</b> chứ không đợi tới lúc học sinh mở bài. Sinh muộn thì một đề thiếu
    /// câu hỏi sẽ nổ ra trước mặt học sinh, giữa giờ kiểm tra, và người duy nhất sửa được lại
    /// không có mặt ở đó. Sinh sớm khiến thao tác giao bài chậm hơn một chút, đổi lại lỗi rơi
    /// đúng vào lúc giáo viên đang ngồi trước màn hình và còn sửa được.
    /// </para>
    /// <para>
    /// Khi tắt <c>randomizePerStudent</c>, lượt đầu tiên tạo bản đề chung rồi những lượt sau nhận
    /// lại chính nó — cả lớp một bộ câu, đúng như cờ đó hứa hẹn.
    /// </para>
    /// </remarks>
    private async Task<Result> GeneratePapersAsync(
        Exam exam,
        IReadOnlyList<ExamAssignment> assignments,
        CancellationToken cancellationToken)
    {
        foreach (var assignment in assignments)
        {
            var paper = await _generator.GetOrCreatePaperAsync(exam, assignment, cancellationToken);
            if (paper.IsFailure)
            {
                return Result.Failure(paper.Error!, paper.ErrorCode);
            }

            // Bản đề chung dùng lại cho mọi em, nên chỉ cần sinh một lần. Ghi xuống ngay để lượt
            // sau tìm thấy nó thay vì sinh thêm một bản nữa.
            if (!exam.RandomizePerStudent)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                break;
            }
        }

        return Result.Success();
    }

    /// <summary>Thông báo cho những em <b>mới</b> được giao.</summary>
    /// <remarks>
    /// Chỉ em mới, không nhắc lại em đã có bài từ trước: giáo viên bấm giao thêm vài em vào cuối
    /// buổi mà cả lớp cùng nhận thông báo thì lần sau không ai buồn đọc nữa.
    ///
    /// Đây mới là bản ghi trong database. Việc đẩy sự kiện <c>ExamAssigned</c> qua SignalR thuộc
    /// bước 8, và phải xảy ra <b>sau khi commit</b> — bắn trước thì có lúc học sinh nhận thông báo
    /// về một bài mà transaction sau đó cuộn lại.
    /// </remarks>
    private async Task AddNotificationsAsync(
        Exam exam,
        IReadOnlyList<ExamAssignment> assignments,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var notifications = assignments.Select(assignment => new Notification
        {
            RecipientUserId = assignment.StudentId,
            Type = NotificationType.ExamAssigned,
            Title = "Bạn có bài kiểm tra mới",
            Message = $"Giáo viên đã giao bài \"{exam.Title}\" cho bạn. Hãy hoàn thành trước hạn nộp.",
            Link = "/hoc-sinh/bai-kiem-tra",
            TargetType = nameof(Exam),
            TargetId = exam.Id,
            IsRead = false,
            CreatedAt = now
        });

        await _unitOfWork.Notifications.AddRangeAsync(notifications, cancellationToken);
    }

    /// <summary>Đề mà người gọi được <b>đọc</b>: giáo viên đọc đề mình tạo, quản trị viên đọc mọi đề.</summary>
    private async Task<Result<Exam>> FindReadableExamAsync(int examId, CancellationToken cancellationToken)
    {
        var exam = await _unitOfWork.Exams.GetByIdAsync(examId, cancellationToken);

        if (exam is null || (!IsAdmin && exam.CreatedByUserId != CurrentUserId))
        {
            return Result.NotFound<Exam>(ExamNotFoundMessage);
        }

        return Result.Success(exam);
    }

    /// <summary>Đề mà người gọi được <b>giao</b>: chỉ chính giáo viên đã tạo.</summary>
    private async Task<Result<Exam>> FindOwnedExamAsync(int examId, CancellationToken cancellationToken)
    {
        if (IsAdmin)
        {
            return Result.Forbidden<Exam>(AdminCannotAssignMessage);
        }

        var exam = await _unitOfWork.Exams.GetByIdAsync(examId, cancellationToken);

        if (exam is null || exam.CreatedByUserId != CurrentUserId)
        {
            return Result.NotFound<Exam>(ExamNotFoundMessage);
        }

        return Result.Success(exam);
    }
}
