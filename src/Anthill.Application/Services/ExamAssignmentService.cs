using Anthill.Application.Common;
using Anthill.Application.DTOs.ExamAssignments;
using Anthill.Application.Interfaces.Realtime;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

/// <summary>
/// Cài đặt <see cref="IExamAssignmentService"/> — giao bài theo lớp của §4.6.
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
    private const string ClassNotAssignedMessage = "Lớp này chưa được giao bài kiểm tra này.";

    private const string AdminCannotAssignMessage =
        "Quản trị viên chỉ xem được bài kiểm tra; việc giao bài thuộc về giáo viên phụ trách môn.";

    private const string DraftCannotAssignMessage =
        "Bài kiểm tra này còn là bản nháp nên chưa giao được. Hãy xuất bản trước.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IExamGeneratorService _generator;
    private readonly IRealtimeNotificationService _realtime;
    private readonly IExamScheduleNotifier _scheduleNotifier;

    public ExamAssignmentService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IExamGeneratorService generator,
        IRealtimeNotificationService realtime,
        IExamScheduleNotifier scheduleNotifier)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _generator = generator;
        _realtime = realtime;
        _scheduleNotifier = scheduleNotifier;
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

        // Transaction mở ngay từ đây và bắt đầu bằng khoá dòng đề — cùng khoá ExamService.UpdateAsync lấy
        // trước khi sửa. Mọi phép kiểm bên dưới (nháp hay chưa, thời lượng, cờ randomizePerStudent) và bước
        // sinh bản đề đều đọc cấu hình đề; không khoá thì một lần sửa commit xen giữa là lô này sinh bản đề
        // theo cấu hình cũ, hoặc lần sửa đếm "chưa có lượt giao" rồi bỏ bản đề mà lô này vừa sinh. Các
        // nhánh trả về sớm chỉ đọc, nên để transaction tự cuộn lại khi rời khối using là đủ.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        if (await _unitOfWork.Exams.LockForEditAsync(exam.Id, cancellationToken) is null)
        {
            return Result.NotFound<AssignExamResponse>(ExamNotFoundMessage);
        }

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

        // Đề theo danh sách có đúng một bộ câu do giáo viên duyệt; chưa có cách chia nó thành nhiều đề nhỏ cho
        // từng em, nên cờ này chưa dùng được. Từ chối rõ ràng thay vì nhận rồi lặng lẽ bỏ qua.
        if (exam.UsesQuestionList && request.RandomizePerStudent == true)
        {
            return Result.Failure<AssignExamResponse>(
                "Đề soạn theo danh sách câu hỏi dùng chung một bộ câu cho cả lớp, chưa hỗ trợ sinh đề riêng "
                + "cho từng học sinh. Các em vẫn được trộn thứ tự câu và đáp án nếu đề bật tuỳ chọn trộn.");
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

        // Lớp đã khoá là lớp chỉ đọc: không giao thêm bài. Từ chối rõ ràng, nêu tên lớp, thay vì lặng lẽ bỏ lớp đó ra và để
        // giáo viên tưởng cả lô đã được giao.
        var lockedGroups = targets.OwnedGroups.Where(group => group.IsLocked).Select(group => group.Name).ToList();
        if (lockedGroups.Count > 0)
        {
            return Result.Conflict<AssignExamResponse>(
                lockedGroups.Count == 1
                    ? $"Lớp {lockedGroups[0]} đã khoá nên không giao thêm bài được. Hãy bỏ lớp đó ra khỏi lựa chọn."
                    : $"Các lớp {string.Join(", ", lockedGroups)} đã khoá nên không giao thêm bài được. Hãy bỏ các lớp đó ra khỏi lựa chọn.");
        }

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

        // Khung giờ riêng của từng lớp trong Classes, chỉ với lớp giáo viên này được giao bài. Kiểm thời lượng
        // ở đây — sau khi biết tên lớp — để câu từ chối chỉ đúng lớp nào đang đặt khung quá ngắn; lớp ngoài
        // phạm vi đã bị loại và được đếm, khung giờ của nó không bao giờ được ghi nên không cần kiểm.
        var classWindows = new Dictionary<int, (DateTime? OpenAt, DateTime? CloseAt)>();
        foreach (var target in request.Classes ?? [])
        {
            var group = targets.OwnedGroups.FirstOrDefault(owned => owned.Id == target.GroupId);
            if (group is null)
            {
                continue;
            }

            if (target.OpenAt is { } classOpen && target.CloseAt is { } classClose
                && !AssignmentSchedule.FitsDuration(classOpen, classClose, exam.DurationMinutes))
            {
                return Result.Failure<AssignExamResponse>(
                    $"Đề này cho làm bài {exam.DurationMinutes} phút, dài hơn khung giờ vừa chọn cho lớp {group.Name}. "
                    + "Hãy nới khung giờ của lớp đó ra, hoặc rút ngắn thời gian làm bài ở phần sửa đề.");
            }

            classWindows[group.Id] = (target.OpenAt.ToUtc(), target.CloseAt.ToUtc());
        }

        // Gửi lại một lớp kèm khung giờ là cách giáo viên sửa giờ cho lớp đó. Chỉ đụng tới những em được
        // giao QUA lớp ấy và chưa bắt đầu làm bài. Lớp trong GroupIds mà request không kèm khung giờ thì
        // không xoá khung giờ nào: giao bổ sung một em vào lớp mà xoá luôn giờ riêng của cả lớp là mất dữ
        // liệu trong im lặng. Lớp trong Classes thì nói trọn khung giờ của nó, kể cả "theo lịch đề".
        //
        // Không gọi Update() trên các bản ghi này: DbSet.Update đi hết đồ thị và đánh dấu luôn Attempt đã
        // Include là Modified — ghi đè một bản cũ của lượt đang làm lên những lần lưu nháp đồng thời.
        // Theo dõi thay đổi tự nhận ra OpenAt/CloseAt đã đổi.
        // Khoá các dòng giao bài trước khi đọc chúng, cùng thứ tự khoá với lần sửa đề (dòng đề trước, dòng
        // giao bài sau). Em bấm "Vào thi" phải cầm khoá trên dòng của mình (LockForStartAsync), nên hoặc em
        // vào trước và lần gửi lại này thấy em đã bắt đầu, hoặc em phải chờ. Không khoá thì một em vừa vào
        // thi bị dời khung giờ dưới tay — và nhận một thông báo "giờ đã đổi" trong lúc đang làm bài.
        var ownedGroupIds = targets.OwnedGroupIds;

        if (ownedGroupIds.Count > 0)
        {
            await _unitOfWork.ExamAssignments.LockByExamAsync(exam.Id, cancellationToken);
        }

        IReadOnlyList<ExamAssignment> viaGroupRows = ownedGroupIds.Count == 0
            ? []
            : await _unitOfWork.ExamAssignments.GetByExamViaGroupsAsync(exam.Id, ownedGroupIds, cancellationToken);

        var rowsByGroup = viaGroupRows
            .GroupBy(row => row.AssignedViaGroupId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var now = DateTime.UtcNow;

        // Khung giờ lần này đòi cho một lớp: khung riêng của lớp, không thì cặp giờ chung của request. Không có
        // gì thì không đụng tới giờ của lớp.
        (DateTime? OpenAt, DateTime? CloseAt)? WindowRequestedFor(int groupId)
        {
            if (classWindows.TryGetValue(groupId, out var classWindow))
            {
                return classWindow;
            }

            return request.OpenAt.HasValue && request.CloseAt.HasValue ? (request.OpenAt, request.CloseAt) : null;
        }

        // Kiểm hết các lớp trước khi đổi giờ lớp nào, để một lớp bị chặn không bỏ lại những lớp đã đổi dở. Chỉ
        // tính em chưa bắt đầu và khung giờ thật sự đổi: gửi lại đúng khung cũ để giao bổ sung vẫn làm được.
        foreach (var groupId in ownedGroupIds)
        {
            if (!rowsByGroup.TryGetValue(groupId, out var groupRows) || WindowRequestedFor(groupId) is not { } requested)
            {
                continue;
            }

            foreach (var row in groupRows)
            {
                if (row.Attempt is not null || !AssignmentSchedule.WindowWouldChange(row, requested.OpenAt, requested.CloseAt))
                {
                    continue;
                }

                var (rowOpenAt, rowCloseAt) = AssignmentSchedule.Resolve(row, exam);
                if (AssignmentSchedule.IsScheduleLocked(rowOpenAt, rowCloseAt, now))
                {
                    var className = targets.OwnedGroups.First(group => group.Id == groupId).Name;
                    return Result.Conflict<AssignExamResponse>(
                        ScheduleLockMessage.For(ScheduleLockMessage.ClassSubject(className), rowOpenAt, now));
                }
            }
        }

        var rescheduled = AssignmentSchedule.RescheduleOutcome.None;
        foreach (var groupId in ownedGroupIds)
        {
            if (!rowsByGroup.TryGetValue(groupId, out var rows) || WindowRequestedFor(groupId) is not { } window)
            {
                continue;
            }

            var outcome = AssignmentSchedule.Reschedule(rows, window.OpenAt, window.CloseAt);

            rescheduled = new AssignmentSchedule.RescheduleOutcome(
                rescheduled.Updated + outcome.Updated,
                rescheduled.KeptStarted + outcome.KeptStarted,
                [.. rescheduled.Changes, .. outcome.Changes]);
        }

        var response = new AssignExamResponse
        {
            AssignedCount = newcomers.Count,
            SkippedAlreadyAssigned = alreadyAssigned.Count,
            SkippedLocked = targets.LockedCount,
            SkippedRetired = targets.RetiredCount,
            SkippedOutOfScope = targets.OutOfScopeCount,
            SkippedOutOfScopeGroups = targets.OutOfScopeGroupCount,
            ScheduleUpdatedCount = rescheduled.Updated,
            ScheduleKeptStartedCount = rescheduled.KeptStarted
        };

        var flagChanged = requestedRandomize is not null && exam.RandomizePerStudent != requestedRandomize;

        if (newcomers.Count == 0 && !flagChanged && rescheduled.Updated == 0)
        {
            // Không có gì để ghi. Vẫn trả 200 kèm các con số: người dùng cần biết vì sao không ai
            // được giao, và "đã giao hết rồi" là câu trả lời hợp lệ chứ không phải lỗi.
            return Result.Success(response);
        }

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

            // Không gọi Exams.Update(exam): đề đang được theo dõi từ lúc khoá, và các dòng giao bài vừa khoá
            // đã được EF gắn vào exam.Assignments. Update() đi hết đồ thị đó và đánh dấu cả những dòng — kể
            // cả lượt làm bài đã nạp kèm — là đã sửa, tức ghi đè một bản cũ lên những lần lưu nháp đồng thời.
        }

        // Những em vừa bị dời giờ phải được báo, kể cả khi giáo viên không tích "gửi thông báo": ô tích đó
        // nói về việc giao bài mới, còn đây là giờ thi của em vừa dịch đi — im lặng ở đây là em đến muộn.
        var scheduleNotices = await StageScheduleNoticesAsync(exam, rescheduled.Changes, now, cancellationToken);

        if (newcomers.Count == 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _scheduleNotifier.CollectUnreadCountsAsync(scheduleNotices, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _scheduleNotifier.PublishAsync(scheduleNotices, cancellationToken);
            return Result.Success(response);
        }

        // Khung giờ lớp đang theo, cho những lớp trong GroupIds được gửi lại mà không kèm giờ. Không có nó
        // thì em vừa được xếp vào 12A1 nhận lịch chung cả tuần của đề trong khi cả lớp làm tiết 2 thứ Ba.
        // Tính sau bước dời giờ ở trên, trên chính các bản ghi đã được cập nhật trong bộ nhớ.
        var inheritedWindows = rowsByGroup.ToDictionary(
            group => group.Key,
            group => AssignmentSchedule.RepresentativeWindow(group.Value.Select(row => new AssignmentSchedule.WindowSample(
                row.OpenAt,
                row.CloseAt,
                Started: row.Attempt is not null,
                Count: 1,
                LatestAssignedAt: row.AssignedAt))));

        var assignments = newcomers
            .Select(student =>
            {
                // Mọi em đều đến từ một lớp được chọn trong lần này: bài chỉ giao theo lớp. Ghi lại lớp đó để
                // lần dời giờ cả lớp sau này tìm lại đúng những em được giao qua lớp ấy, kể cả khi em đã
                // chuyển sang lớp khác (GetByExamViaGroupsAsync khớp theo AssignedViaGroupId).
                var viaGroupId = targets.ViaGroup[student.Id];

                var (openAt, closeAt) = AssignmentSchedule.WindowForNewAssignment(
                    viaGroupId,
                    (request.OpenAt, request.CloseAt),
                    classWindows,
                    inheritedWindows);

                return new ExamAssignment
                {
                    ExamId = exam.Id,
                    StudentId = student.Id,
                    AssignedAt = now,
                    CreatedAt = now,
                    AssignedViaGroupId = viaGroupId,

                    // Lịch riêng của lượt giao này. NULL nghĩa là theo lịch chung của đề — không chép
                    // sẵn lịch đề vào đây, vì chép rồi thì sửa lịch đề sẽ không còn lan tới những lớp
                    // vốn không đặt giờ riêng. Quy về UTC ngay để giá trị trong bộ nhớ khớp với thứ sẽ
                    // nằm trong database, và khớp với phép so của Reschedule.
                    OpenAt = openAt.ToUtc(),
                    CloseAt = closeAt.ToUtc()
                };
            })
            .ToList();

        await _unitOfWork.ExamAssignments.AddRangeAsync(assignments, cancellationToken);

        // Cần id thật của assignment trước khi sinh đề: hạt giống của bản đề riêng dựng từ id đó.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var generated = await _generator.GeneratePapersAsync(exam, assignments, cancellationToken);
        if (generated.IsFailure)
        {
            // Không sinh nổi đề thì không giao. Cuộn lại cả lô thay vì để lại những assignment
            // trỏ vào một bản đề không tồn tại.
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<AssignExamResponse>(generated.Error!, generated.ErrorCode);
        }

        // Em được giao vào một khung giờ đã đóng thì không còn gì để "hoàn thành trước hạn nộp": báo là báo
        // sai, nên bỏ qua cả thông báo lẫn sự kiện. Khi giáo viên mở lại khung đó, lần dời giờ sẽ báo em.
        var notified = request.Notify
            ? assignments.Where(assignment => !IsWindowClosed(exam, assignment, now)).ToList()
            : new List<ExamAssignment>();

        if (notified.Count > 0)
        {
            await AddNotificationsAsync(exam, notified, now, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Mọi lượt đọc phục vụ thông báo nằm TRONG transaction. Đọc sau commit là những lượt đi
        // database không còn lớp bảo vệ nào: một trục trặc thoáng qua sẽ biến một lô giao bài đã
        // thành công thành lỗi trả về cho giáo viên. Sau commit chỉ còn đúng việc bắn sự kiện,
        // mà việc đó tự nuốt lỗi ở tầng cài đặt.
        var teacherName = string.Empty;
        var unreadCounts = new Dictionary<int, int>();

        if (notified.Count > 0)
        {
            var teacher = await _unitOfWork.Users.GetByIdAsync(exam.CreatedByUserId, cancellationToken);
            teacherName = teacher?.FullName ?? string.Empty;

            foreach (var assignment in notified)
            {
                unreadCounts[assignment.StudentId] =
                    await _unitOfWork.Notifications.CountUnreadAsync(assignment.StudentId, cancellationToken);
            }
        }

        await _scheduleNotifier.CollectUnreadCountsAsync(scheduleNotices, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // SAU khi commit, không sớm hơn một dòng nào. Bắn trước commit thì có lúc học sinh nhận
        // thông báo về một bài mà transaction ngay sau đó cuộn lại — em mở ứng dụng ra và không
        // thấy gì (§7).
        if (notified.Count > 0)
        {
            await PublishAssignedAsync(exam, notified, teacherName, unreadCounts, cancellationToken);
        }

        await _scheduleNotifier.PublishAsync(scheduleNotices, cancellationToken);

        return Result.Success(response);
    }

    /// <summary>Thêm thông báo đổi giờ cho những lượt giao vừa bị dời sang khung khác.</summary>
    /// <remarks>
    /// Gọi trong transaction đang khoá các dòng giao bài, sau khi khung giờ mới đã được gán trong bộ nhớ:
    /// <see cref="ScheduleNotice.FromReschedule"/> so khung giờ có hiệu lực trước và sau, nên đặt giờ riêng
    /// trùng khít lịch đề không sinh thông báo nào.
    /// </remarks>
    private async Task<ScheduleNoticeBatch> StageScheduleNoticesAsync(
        Exam exam,
        IReadOnlyList<AssignmentSchedule.RescheduleChange> changes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (changes.Count == 0)
        {
            return ScheduleNoticeBatch.Empty;
        }

        var notices = changes
            .Select(change => ScheduleNotice.FromReschedule(change, exam, now))
            .ToList();

        return await _scheduleNotifier.StageExamUpdatedAsync(exam, notices, now, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Cùng thứ tự khoá với giao bài và sửa đề: dòng đề trước, dòng giao bài sau. Em bấm "Vào thi" giữ khoá trên
    /// dòng của mình trước khi tạo lượt làm bài, nên hoặc em vào trước và phép đếm bên dưới thấy lượt của em
    /// (em được giữ lại), hoặc em phải chờ lệnh thu hồi này commit (và không còn gì để vào nữa). Không khoá thì
    /// một em vừa bấm "Vào thi" bị gỡ bài ngay dưới tay — mất luôn phần bài em vừa bắt đầu.
    /// </para>
    /// <para>
    /// Chỉ gỡ những em được giao <b>qua lớp này</b> (<c>AssignedViaGroupId</c>), khớp cùng cách lần dời giờ cả
    /// lớp tìm các em của lớp. Bản đề riêng của từng em đi theo nhờ cascade; bản đề chung của cả lớp không gắn
    /// với assignment nào nên không bị đụng tới — những em khác vẫn đang làm trên đó.
    /// </para>
    /// </remarks>
    public async Task<Result<RevokeClassResponse>> RevokeClassAsync(
        int examId,
        int groupId,
        CancellationToken cancellationToken = default)
    {
        var found = await FindOwnedExamAsync(examId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<RevokeClassResponse>(found.Error!, found.ErrorCode);
        }

        // Cùng phạm vi với giao bài: chỉ giáo viên đang dạy một lớp đã công bố mới đụng được tới bài của lớp đó. Lớp mình không dạy
        // (hay không có) trả cùng một câu 404 như lớp chưa được giao đề, để không dò được lớp của người khác.
        var teachable = await _unitOfWork.StudentGroups.GetTeachableByIdsAsync([groupId], CurrentUserId, cancellationToken);
        if (teachable.Count == 0)
        {
            return Result.NotFound<RevokeClassResponse>(ClassNotAssignedMessage);
        }

        // Lớp đã khoá là lớp chỉ đọc: không giao thêm và cũng không thu hồi bài.
        if (teachable[0].IsLocked)
        {
            return Result.Conflict<RevokeClassResponse>($"Lớp {teachable[0].Name} đã khoá nên không thu hồi bài được.");
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        if (await _unitOfWork.Exams.LockForEditAsync(examId, cancellationToken) is null)
        {
            return Result.NotFound<RevokeClassResponse>(ExamNotFoundMessage);
        }

        await _unitOfWork.ExamAssignments.LockByExamAsync(examId, cancellationToken);

        var rows = await _unitOfWork.ExamAssignments.GetByExamViaGroupsAsync(examId, [groupId], cancellationToken);

        if (rows.Count == 0)
        {
            return Result.NotFound<RevokeClassResponse>(ClassNotAssignedMessage);
        }

        var revocable = rows.Where(row => row.Attempt is null).ToList();
        var started = rows.Count - revocable.Count;

        if (revocable.Count == 0)
        {
            return Result.Conflict<RevokeClassResponse>(
                "Mọi em của lớp này đã bắt đầu làm bài nên không thu hồi được nữa. "
                + "Thu hồi lúc này là xoá luôn phần bài các em đã làm.");
        }

        _unitOfWork.ExamAssignments.RemoveRange(revocable);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new RevokeClassResponse
        {
            RevokedCount = revocable.Count,
            KeptStartedCount = started
        });
    }

    /// <summary>Kết quả gom và sàng lọc danh sách học sinh được chọn.</summary>
    private sealed record Targets
    {
        /// <summary>Những em thật sự giao được: có thật, đúng vai trò, đang hoạt động, thuộc lớp của giáo viên.</summary>
        public required IReadOnlyList<User> InScope { get; init; }

        /// <summary>Em nào đến từ lớp nào: mọi em trong <see cref="InScope"/> đều có mặt.</summary>
        public required IReadOnlyDictionary<int, int> ViaGroup { get; init; }

        /// <summary>Các lớp trong request mà giáo viên này được giao bài — cũng là các lớp được phép sửa khung giờ.</summary>
        public required IReadOnlyList<StudentGroup> OwnedGroups { get; init; }

        public IReadOnlyList<int> OwnedGroupIds => OwnedGroups.Select(group => group.Id).ToList();

        public required int LockedCount { get; init; }

        /// <summary>Số em đang chờ xoá — đếm riêng khỏi <see cref="LockedCount"/>.</summary>
        /// <remarks>
        /// Tài khoản đã xoá hẳn không bao giờ vào đây: ẩn danh hoá gỡ em khỏi lớp, nên em rơi vào
        /// <see cref="OutOfScopeCount"/> trước.
        /// </remarks>
        public required int RetiredCount { get; init; }

        /// <summary>
        /// Số id bị loại vì không thuộc nhóm nào của giáo viên này — <b>kể cả</b> id không trỏ tới tài
        /// khoản học sinh nào. Xem <c>AssignExamResponse.SkippedOutOfScope</c>: hai lý do trả lời khác
        /// nhau là một cách dò xem id nào là học sinh thật của lớp người khác.
        /// </summary>
        public required int OutOfScopeCount { get; init; }

        /// <summary>Số <b>nhóm lớp</b> bị loại vì không do giáo viên này quản lý.</summary>
        public required int OutOfScopeGroupCount { get; init; }
    }

    /// <summary>
    /// Gom học sinh của các lớp đã chọn, khử trùng lặp, rồi sàng bốn lớp.
    /// </summary>
    /// <remarks>
    /// Thứ tự sàng có ý nghĩa: <b>ngoài phạm vi</b> (id không tồn tại đếm chung vào đây) → <b>đã rời
    /// trường</b> → <b>đang khoá</b>. Một em vừa ngoài phạm vi vừa bị khoá thì chỉ được đếm một lần, ở
    /// lý do đầu tiên chạm tới — xem <see cref="AssignmentTargetFilter"/>.
    /// </remarks>
    private async Task<Targets> ResolveTargetsAsync(
        AssignExamRequest request,
        CancellationToken cancellationToken)
    {
        // Người nhận chỉ đến từ các lớp được chọn. HashSet để một em học hai lớp cùng được chọn vẫn chỉ được giao một lần.
        var requestedIds = new HashSet<int>();
        // Hai danh sách lớp gộp làm một ở bước chọn người nhận: khác nhau chỉ ở khung giờ, còn phạm vi, đếm
        // lớp bị loại và ghi dấu "giao qua lớp nào" là như nhau. Validator đã chặn một lớp nằm ở cả hai.
        var requestedGroupIds = (request.GroupIds ?? [])
            .Concat((request.Classes ?? []).Select(target => target.GroupId))
            .Distinct()
            .ToList();

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

        // Ghi lại em nào đến từ lớp nào ngay tại đây. Tra ngược qua danh sách lớp hiện tại của em về sau là sai:
        // em học nhiều lớp, và chuyển lớp hay bị gỡ khỏi lớp giữa kỳ thì danh sách đó đổi theo, lúc ấy không còn
        // cách nào biết bài này vốn được giao cho em với tư cách thành viên lớp nào — mà đó là thứ quyết định em
        // làm bài vào khung giờ nào.
        var viaGroup = new Dictionary<int, int>();

        foreach (var group in ownedGroups)
        {
            var members = await _unitOfWork.Users.GetStudentsByGroupAsync(group.Id, cancellationToken);
            foreach (var member in members)
            {
                requestedIds.Add(member.Id);

                // Em học nhiều lớp và cùng lúc lọt vào hai lớp được chọn thì lấy lớp đầu tiên — nhất quán giữa
                // các lần chạy vì ownedGroups đã sắp thứ tự. Em đã được giao đề này qua một lớp khác từ trước thì
                // không được giao lại (alreadyAssigned), nên khung giờ của lớp đầu tiên giao cho em là khung giờ thắng.
                viaGroup.TryAdd(member.Id, group.Id);
            }
        }

        if (requestedIds.Count == 0)
        {
            return new Targets
            {
                InScope = [],
                ViaGroup = viaGroup,
                OwnedGroups = ownedGroups,
                LockedCount = 0,
                RetiredCount = 0,
                OutOfScopeCount = 0,
                OutOfScopeGroupCount = outOfScopeGroups
            };
        }

        // Chỉ trả về id có thật và đúng vai trò Student. Chênh lệch số lượng chính là số id lạ, và nó
        // là điểm xuất phát của bộ đếm "ngoài phạm vi": id không tồn tại và học sinh của lớp người khác
        // phải cho ra cùng một câu trả lời, nếu không thì đếm chênh lệch giữa hai lần gọi là đủ để dò
        // ra id nào là học sinh thật (xem AssignExamResponse.SkippedOutOfScope).
        var students = await _unitOfWork.Users.GetStudentsByIdsAsync(requestedIds, cancellationToken);
        var outOfScope = requestedIds.Count - students.Count;

        // Phạm vi: em phải thuộc một lớp mà giáo viên này được phân công dạy. Không có hàng rào
        // này thì đoán id là giao được bài cho lớp của đồng nghiệp.
        var myGroupIds = (await _unitOfWork.StudentGroups.GetForTeacherAsync(CurrentUserId, search: null, cancellationToken))
            .Select(group => group.Id)
            .ToHashSet();

        var inScope = new List<User>(students.Count);
        var locked = 0;
        var retired = 0;

        foreach (var student in students)
        {
            switch (AssignmentTargetFilter.Classify(student, myGroupIds))
            {
                case AssignmentSkipReason.OutOfScope:
                    outOfScope++;
                    break;
                case AssignmentSkipReason.Locked:
                    locked++;
                    break;
                case AssignmentSkipReason.Retired:
                    retired++;
                    break;
                default:
                    inScope.Add(student);
                    break;
            }
        }

        return new Targets
        {
            InScope = inScope,
            ViaGroup = viaGroup,
            OwnedGroups = ownedGroups,
            LockedCount = locked,
            RetiredCount = retired,
            OutOfScopeCount = outOfScope,
            OutOfScopeGroupCount = outOfScopeGroups
        };
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
            Link = ScheduleNoticeText.StudentExamLink(assignment.Id),
            TargetType = nameof(Exam),
            TargetId = exam.Id,
            IsRead = false,
            CreatedAt = now
        });

        await _unitOfWork.Notifications.AddRangeAsync(notifications, cancellationToken);
    }

    /// <summary>Khung giờ có hiệu lực của lượt giao này đã đóng chưa. Bằng giờ đóng vẫn còn mở.</summary>
    private static bool IsWindowClosed(Exam exam, ExamAssignment assignment, DateTime now)
    {
        var (openAt, closeAt) = AssignmentSchedule.Resolve(assignment, exam);

        return ExamMappingExtensions.ResolveWindowLifecycle(isDraft: false, openAt, closeAt, now)
               == ExamLifecycle.Closed;
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
