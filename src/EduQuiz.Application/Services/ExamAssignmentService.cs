using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.ExamAssignments;
using EduQuiz.Application.Interfaces.Realtime;
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
/// quan trọng hơn: không xoá mất lượt làm bài hay điểm của em đã làm rồi. Gửi lại chỉ có thể đổi
/// khung giờ của em chưa bắt đầu, không bao giờ đụng trạng thái hay điểm.</item>
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
    private readonly IRealtimeNotificationService _realtime;

    public ExamAssignmentService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IExamGeneratorService generator,
        IRealtimeNotificationService realtime)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _generator = generator;
        _realtime = realtime;
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

        // Nằm ở đây chứ không ở validator vì thời lượng đến từ đề — validator không nhìn thấy nó. Kiểm cả
        // khi đây là lần gửi lại để sửa giờ cho một lớp đã giao, vì khung giờ đó cũng sẽ được ghi xuống.
        if (request.OpenAt is { } requestedOpen && request.CloseAt is { } requestedClose
            && !AssignmentSchedule.FitsDuration(requestedOpen, requestedClose, exam.DurationMinutes))
        {
            return Result.Failure<AssignExamResponse>(
                $"Đề này cho làm bài {exam.DurationMinutes} phút, dài hơn khung giờ riêng vừa chọn. "
                + "Hãy nới khung giờ ra, hoặc rút ngắn thời gian làm bài ở phần sửa đề.");
        }

        // Cờ này thuộc về đề chứ không thuộc về từng assignment. Đổi nó khi đề đã giao một phần
        // thì mọi assignment cũ đổi luôn cách tra bản đề — bản chung sang bản riêng hoặc ngược lại
        // — trong khi chỉ những em mới được sinh đề. Kết quả là một lớp mà nửa số em không còn
        // đường tới bản đề nào. Chốt lại: khi đã có người được giao thì cờ không đổi được nữa.
        var existingCount = await _unitOfWork.ExamAssignments.CountByExamAsync(exam.Id, cancellationToken);

        // null nghia la "giu nguyen", nen chi so sanh khi client thuc su gui mot gia tri.
        var requestedRandomize = request.RandomizePerStudent;

        if (existingCount > 0 && requestedRandomize is not null && exam.RandomizePerStudent != requestedRandomize)
        {
            return Result.Conflict<AssignExamResponse>(
                exam.RandomizePerStudent
                    ? "Đề này đã giao theo kiểu mỗi học sinh một đề riêng nên không đổi sang đề chung được nữa. "
                      + "Hãy thu hồi hết rồi giao lại, hoặc tạo một đề mới."
                    : "Đề này đã giao theo kiểu cả lớp chung một đề nên không đổi sang đề riêng được nữa. "
                      + "Hãy thu hồi hết rồi giao lại, hoặc tạo một đề mới.");
        }

        var targets = await ResolveTargetsAsync(request, cancellationToken);

        // Trần thật, tính sau khi đã bung các nhóm ra. Validator chỉ chặn được phần thô vì nó
        // không biết mỗi nhóm có bao nhiêu em.
        if (targets.InScope.Count > Validators.AssignExamRequestValidator.MaxTargetsPerRequest)
        {
            return Result.Failure<AssignExamResponse>(
                $"Lựa chọn này gồm {targets.InScope.Count} học sinh, vượt trần "
                + $"{Validators.AssignExamRequestValidator.MaxTargetsPerRequest} cho một lần giao. "
                + "Hãy chia thành nhiều lần.");
        }

        var alreadyAssigned = (await _unitOfWork.ExamAssignments.GetAssignedStudentIdsAsync(
            examId,
            targets.InScope.Select(student => student.Id),
            cancellationToken)).ToHashSet();

        var newcomers = targets.InScope
            .Where(student => !alreadyAssigned.Contains(student.Id))
            .ToList();

        // Gửi lại một lớp kèm khung giờ là cách giáo viên sửa giờ cho lớp đó. Chỉ đụng tới những em được
        // giao QUA lớp ấy và chưa bắt đầu làm bài. Request không kèm khung giờ thì không xoá khung giờ nào:
        // giao bổ sung một em vào lớp mà xoá luôn giờ riêng của cả lớp là mất dữ liệu trong im lặng.
        //
        // Không gọi Update() trên các bản ghi này: DbSet.Update đi hết đồ thị và đánh dấu luôn Attempt đã
        // Include là Modified — ghi đè một bản cũ của lượt đang làm lên những lần lưu nháp đồng thời.
        // Theo dõi thay đổi tự nhận ra OpenAt/CloseAt đã đổi.
        var rescheduled = new AssignmentSchedule.RescheduleOutcome(0, 0);
        if (request.OpenAt is { } newOpenAt && request.CloseAt is { } newCloseAt && targets.OwnedGroupIds.Count > 0)
        {
            var viaGroupRows = await _unitOfWork.ExamAssignments.GetByExamViaGroupsAsync(
                exam.Id,
                targets.OwnedGroupIds,
                cancellationToken);

            rescheduled = AssignmentSchedule.Reschedule(viaGroupRows, newOpenAt, newCloseAt);
        }

        var response = new AssignExamResponse
        {
            AssignedCount = newcomers.Count,
            SkippedAlreadyAssigned = alreadyAssigned.Count,
            SkippedLocked = targets.LockedCount,
            SkippedNotFound = targets.NotFoundCount,
            SkippedOutOfScope = targets.OutOfScopeCount,
            SkippedOutOfScopeGroups = targets.OutOfScopeGroupCount,
            ScheduleUpdatedCount = rescheduled.Updated,
            ScheduleKeptStartedCount = rescheduled.KeptStarted
        };

        var now = DateTime.UtcNow;
        var flagChanged = requestedRandomize is not null && exam.RandomizePerStudent != requestedRandomize;

        if (newcomers.Count == 0 && !flagChanged && rescheduled.Updated == 0)
        {
            // Không có gì để ghi. Vẫn trả 200 kèm các con số: người dùng cần biết vì sao không ai
            // được giao, và "đã giao hết rồi" là câu trả lời hợp lệ chứ không phải lỗi.
            return Result.Success(response);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        // Ghi cờ BÊN TRONG transaction. Đặt ngoài — kèm SaveChanges riêng — thì nó đã nằm trong
        // database trước khi transaction mở, nên lần rollback phía dưới không cuốn nó theo: sinh
        // đề hỏng, không ai được giao bài, mà cờ vẫn đổi. Lần giao sau đó sẽ bị chính phép kiểm ở
        // đầu hàm từ chối vì "đề đã giao theo kiểu kia rồi", trong khi thực tế chưa giao cho ai.
        //
        // Vẫn phải đặt trước nhánh thoát sớm ở trên: cờ chỉ đặt được ở endpoint này, nên bỏ qua nó
        // khi mọi em đều đã có bài là đánh rơi lựa chọn của giáo viên mà không báo gì.
        if (flagChanged)
        {
            exam.RandomizePerStudent = requestedRandomize!.Value;
            exam.UpdatedAt = now;
            _unitOfWork.Exams.Update(exam);
        }

        if (newcomers.Count == 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(response);
        }

        var assignments = newcomers
            .Select(student => new ExamAssignment
            {
                ExamId = exam.Id,
                StudentId = student.Id,
                AssignedAt = now,
                CreatedAt = now,

                // NULL khi giáo viên chọn đích danh em này thay vì chọn cả lớp.
                AssignedViaGroupId = targets.ViaGroup.TryGetValue(student.Id, out var groupId)
                    ? groupId
                    : null,

                // Lịch riêng của lượt giao này. NULL nghĩa là theo lịch chung của đề — không chép
                // sẵn lịch đề vào đây, vì chép rồi thì sửa lịch đề sẽ không còn lan tới những lớp
                // vốn không đặt giờ riêng. Quy về UTC ngay để giá trị trong bộ nhớ khớp với thứ sẽ
                // nằm trong database, và khớp với phép so của Reschedule.
                OpenAt = request.OpenAt.ToUtc(),
                CloseAt = request.CloseAt.ToUtc()
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

        // Mọi lượt đọc phục vụ thông báo nằm TRONG transaction. Đọc sau commit là những lượt đi
        // database không còn lớp bảo vệ nào: một trục trặc thoáng qua sẽ biến một lô giao bài đã
        // thành công thành lỗi trả về cho giáo viên. Sau commit chỉ còn đúng việc bắn sự kiện,
        // mà việc đó tự nuốt lỗi ở tầng cài đặt.
        var teacherName = string.Empty;
        var unreadCounts = new Dictionary<int, int>();

        if (request.Notify)
        {
            var teacher = await _unitOfWork.Users.GetByIdAsync(exam.CreatedByUserId, cancellationToken);
            teacherName = teacher?.FullName ?? string.Empty;

            foreach (var assignment in assignments)
            {
                unreadCounts[assignment.StudentId] =
                    await _unitOfWork.Notifications.CountUnreadAsync(assignment.StudentId, cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        // SAU khi commit, không sớm hơn một dòng nào. Bắn trước commit thì có lúc học sinh nhận
        // thông báo về một bài mà transaction ngay sau đó cuộn lại — em mở ứng dụng ra và không
        // thấy gì (§7).
        if (request.Notify)
        {
            await PublishAssignedAsync(exam, assignments, teacherName, unreadCounts, cancellationToken);
        }

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

        /// <summary>Em nào đến từ lớp nào. Vắng mặt nghĩa là giáo viên chọn đích danh em đó.</summary>
        public required IReadOnlyDictionary<int, int> ViaGroup { get; init; }

        /// <summary>Các lớp trong request mà giáo viên này được giao bài — cũng là các lớp được phép sửa khung giờ.</summary>
        public required IReadOnlyList<int> OwnedGroupIds { get; init; }

        public required int NotFoundCount { get; init; }

        public required int LockedCount { get; init; }

        /// <summary>Số <b>học sinh</b> không thuộc nhóm nào của giáo viên này.</summary>
        public required int OutOfScopeCount { get; init; }

        /// <summary>Số <b>nhóm lớp</b> bị loại vì không do giáo viên này quản lý.</summary>
        public required int OutOfScopeGroupCount { get; init; }
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
        var requestedIds = new HashSet<int>(request.StudentIds ?? []);
        var requestedGroupIds = (request.GroupIds ?? []).Distinct().ToList();

        // Lớp mà giáo viên này không được phân công bị loại ngay tại đây, và lớp còn ở dạng nháp
        // cũng vậy: repository chỉ trả về lớp thoả cả hai điều kiện, nên một id đoán bừa không kéo
        // theo học sinh nào.
        var ownedGroups = await _unitOfWork.StudentGroups.GetTeachableByIdsAsync(
            requestedGroupIds,
            CurrentUserId,
            cancellationToken);

        // Nhóm bị loại phải được đếm. Bỏ qua trong im lặng nghĩa là giáo viên chọn nhầm id nhóm
        // rồi nhận về một bảng kết quả toàn số không, không có dòng nào giải thích vì sao — đúng
        // sự im lặng mà AssignExamResponse nói là nó sinh ra để tránh.
        var outOfScopeGroups = requestedGroupIds.Count - ownedGroups.Count;

        // Ghi lại em nào đến từ lớp nào ngay tại đây. Tra ngược qua User.GroupId về sau là sai:
        // học sinh chuyển lớp giữa kỳ thì GroupId đổi theo, và lúc đó không còn cách nào biết bài
        // này vốn được giao cho em với tư cách thành viên lớp nào — mà đó là thứ quyết định em làm
        // bài vào khung giờ nào.
        var viaGroup = new Dictionary<int, int>();

        foreach (var group in ownedGroups)
        {
            var members = await _unitOfWork.Users.GetStudentsByGroupAsync(group.Id, cancellationToken);
            foreach (var member in members)
            {
                requestedIds.Add(member.Id);

                // Em thuộc nhiều lớp là chuyện không xảy ra (User.GroupId là khoá đơn), nhưng nếu
                // cùng một em lọt vào hai lớp được chọn thì lấy lớp đầu tiên — nhất quán giữa các
                // lần chạy vì ownedGroups đã sắp thứ tự.
                viaGroup.TryAdd(member.Id, group.Id);
            }
        }

        if (requestedIds.Count == 0)
        {
            return new Targets
            {
                InScope = [],
                ViaGroup = viaGroup,
                OwnedGroupIds = ownedGroups.Select(group => group.Id).ToList(),
                NotFoundCount = 0,
                LockedCount = 0,
                OutOfScopeCount = 0,
                OutOfScopeGroupCount = outOfScopeGroups
            };
        }

        // Chỉ trả về id có thật và đúng vai trò Student. Chênh lệch số lượng chính là số id lạ.
        var students = await _unitOfWork.Users.GetStudentsByIdsAsync(requestedIds, cancellationToken);
        var notFound = requestedIds.Count - students.Count;

        // Phạm vi: em phải thuộc một lớp mà giáo viên này được phân công dạy. Không có hàng rào
        // này thì đoán id là giao được bài cho lớp của đồng nghiệp.
        var myGroupIds = (await _unitOfWork.StudentGroups.GetForTeacherAsync(CurrentUserId, search: null, cancellationToken))
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
            ViaGroup = viaGroup,
            OwnedGroupIds = ownedGroups.Select(group => group.Id).ToList(),
            NotFoundCount = notFound,
            LockedCount = locked,
            OutOfScopeCount = outOfScope,
            OutOfScopeGroupCount = outOfScopeGroups
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

    /// <summary>Bắn sự kiện realtime cho từng em vừa được giao.</summary>
    /// <remarks>
    /// Mỗi em một sự kiện mang <b>đúng assignmentId của chính em</b>. Gửi chung một payload cho cả
    /// lô thì hai mươi chín em nhận id của bạn cùng lớp: liên kết sâu trong thông báo dẫn sang bài
    /// của người khác — và tuy server vẫn chặn ở bước đọc, việc phát tán id của người khác là thứ
    /// không cần thiết phải làm.
    ///
    /// Cũng đẩy lại số chưa đọc, vì bản ghi thông báo vừa được tạo trong transaction: không có nó
    /// thì chấm đỏ trên chuông đứng yên cho tới lần tải trang kế tiếp — mà việc poll đã bị bỏ đi
    /// chính vì sự kiện này (§7).
    ///
    /// Mỗi sự kiện cũng mang khung giờ thật của chính em đó, phân giải với đề đưa vào tường minh: bản
    /// ghi vừa tạo chưa chắc đã được gắn navigation Exam, và bước này chạy sau commit nên một lỗi ở
    /// đây biến một lô giao bài đã ghi xong thành lỗi 500.
    /// </remarks>
    private async Task PublishAssignedAsync(
        Exam exam,
        IReadOnlyList<ExamAssignment> assignments,
        string teacherName,
        IReadOnlyDictionary<int, int> unreadCounts,
        CancellationToken cancellationToken)
    {
        foreach (var assignment in assignments)
        {
            await _realtime.ExamAssignedAsync(
                [assignment.StudentId],
                assignment.ToAssignedEvent(exam, teacherName),
                cancellationToken);

            await _realtime.UnreadCountChangedAsync(
                assignment.StudentId,
                unreadCounts.GetValueOrDefault(assignment.StudentId),
                cancellationToken);
        }
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
