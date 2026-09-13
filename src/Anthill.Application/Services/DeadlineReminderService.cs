using Anthill.Application.Common;
using Anthill.Application.Interfaces.Realtime;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;

namespace Anthill.Application.Services;

/// <summary>Cài đặt <see cref="IDeadlineReminderService"/>.</summary>
/// <remarks>
/// Mỗi em một transaction, như tiến trình tự nộp bài: một dòng hỏng là chuyện của riêng em đó, ba mươi chín
/// em còn lại vẫn phải được nhắc trong lượt quét này.
/// </remarks>
public sealed class DeadlineReminderService : IDeadlineReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotificationService _realtime;

    public DeadlineReminderService(IUnitOfWork unitOfWork, IRealtimeNotificationService realtime)
    {
        _unitOfWork = unitOfWork;
        _realtime = realtime;
    }

    /// <inheritdoc />
    public async Task<Result<int>> SendDueRemindersAsync(
        int batchSize,
        int marginMinutes,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            return Result.Success(0);
        }

        var now = DateTime.UtcNow;

        // Trần của phép lọc thô: không đề nào được làm lâu hơn MaxDurationMinutes, nên không mốc nhắc nào xa
        // hơn con số đó cộng biên. Lấy rộng hơn thì lượt quét kéo về cả những em còn cả tuần nữa mới thi.
        var horizon = DeadlineReminder.LeadTime(Validators.SaveExamRequestValidator.MaxDurationMinutes, marginMinutes);

        var candidates = await _unitOfWork.ExamAssignments.GetDeadlineReminderCandidatesAsync(
            now,
            horizon,
            batchSize,
            cancellationToken);

        var sent = 0;

        foreach (var candidate in candidates)
        {
            if (await SendUnderLockAsync(candidate.Id, marginMinutes, now, cancellationToken))
            {
                sent++;
            }
        }

        return Result.Success(sent);
    }

    /// <summary>Nhắc một em, dưới khoá ghi trên dòng giao bài của em.</summary>
    /// <remarks>
    /// Mọi phép quyết định được làm lại <b>sau</b> khi cầm khoá, trên dòng vừa đọc: giữa lúc lọc thô và lúc
    /// này em có thể đã bấm "Vào thi", giáo viên có thể vừa dời giờ, và một tiến trình quét khác có thể vừa
    /// nhắc xong. Khoá cũng chính là thứ khiến hai instance cùng chạy không gửi hai lời nhắc cho một em:
    /// người đến sau đọc được mốc đã nhắc mà người trước vừa ghi.
    /// </remarks>
    private async Task<bool> SendUnderLockAsync(
        int assignmentId,
        int marginMinutes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var assignment = await _unitOfWork.ExamAssignments.LockForRescheduleAsync(assignmentId, cancellationToken);
        if (assignment?.Exam is not { } exam || assignment.Attempt is not null)
        {
            return false;
        }

        var (openAt, closeAt) = AssignmentSchedule.Resolve(assignment, exam);

        var decision = DeadlineReminder.Decide(
            openAt,
            closeAt,
            exam.DurationMinutes,
            marginMinutes,
            assignment.DeadlineNoticeCloseAt,
            now);

        if (!decision.ShouldSend)
        {
            return false;
        }

        // Không gọi Update(): dòng đã được theo dõi từ lúc khoá, và Update() đi hết đồ thị đã nạp kèm.
        assignment.DeadlineNoticeCloseAt = closeAt.ToUtc();

        await _unitOfWork.Notifications.AddAsync(
            DeadlineReminderText.For(assignment, exam, decision.MinutesRemaining, now),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Đọc số chưa đọc TRONG transaction, như mọi đường sinh thông báo khác: sau commit là một lượt đi
        // database không còn lớp bảo vệ nào.
        var unreadCount = await _unitOfWork.Notifications.CountUnreadAsync(assignment.StudentId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        await PublishAsync(assignment, exam, closeAt, decision.MinutesRemaining, unreadCount, cancellationToken);
        return true;
    }

    /// <summary>Bắn sự kiện realtime. Chỉ gọi sau commit.</summary>
    /// <remarks>
    /// Giáo viên cũng nhận sự kiện này (§7) nhưng <b>không</b> có bản ghi thông báo: bốn mươi em chưa vào thi
    /// là bốn mươi dòng trong hộp thư của một người, và người đó đã có bảng theo dõi hiện đúng con số ấy.
    /// </remarks>
    private async Task PublishAsync(
        ExamAssignment assignment,
        Exam exam,
        DateTime closeAt,
        int minutesRemaining,
        int unreadCount,
        CancellationToken cancellationToken)
    {
        await _realtime.DeadlineSoonAsync(
            assignment.StudentId,
            exam.CreatedByUserId,
            new DeadlineSoonEvent
            {
                ExamId = exam.Id,
                AssignmentId = assignment.Id,
                ExamTitle = exam.Title,
                CloseAt = closeAt.ToUtc(),
                MinutesRemaining = minutesRemaining
            },
            cancellationToken);

        await _realtime.UnreadCountChangedAsync(assignment.StudentId, unreadCount, cancellationToken);
    }
}
