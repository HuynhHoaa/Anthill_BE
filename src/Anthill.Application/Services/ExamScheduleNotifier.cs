using Anthill.Application.Common;
using Anthill.Application.Interfaces.Realtime;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

/// <summary>Cài đặt <see cref="IExamScheduleNotifier"/>.</summary>
public sealed class ExamScheduleNotifier : IExamScheduleNotifier
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotificationService _realtime;

    public ExamScheduleNotifier(IUnitOfWork unitOfWork, IRealtimeNotificationService realtime)
    {
        _unitOfWork = unitOfWork;
        _realtime = realtime;
    }

    /// <inheritdoc />
    public async Task<ScheduleNoticeBatch> StageExamUpdatedAsync(
        Exam exam,
        IEnumerable<ScheduleNotice> notices,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exam);
        ArgumentNullException.ThrowIfNull(notices);

        // Một em một thông báo cho một lần đổi, kể cả khi cùng một em lọt vào danh sách hai lần.
        var pending = notices
            .Where(notice => notice.HasChange)
            .GroupBy(notice => notice.Assignment.Id)
            .Select(group => group.First())
            .ToList();

        if (pending.Count == 0)
        {
            return ScheduleNoticeBatch.Empty;
        }

        var stillEnrolled = (await _unitOfWork.Users.GetStudentsByIdsAsync(
                pending.Select(notice => notice.Assignment.StudentId),
                cancellationToken))
            .Where(student => student.Status is UserStatus.Active or UserStatus.Locked)
            .Select(student => student.Id)
            .ToHashSet();

        var batch = new ScheduleNoticeBatch();
        var notifications = new List<Notification>();

        foreach (var notice in pending.Where(notice => stillEnrolled.Contains(notice.Assignment.StudentId)))
        {
            notifications.Add(ScheduleNoticeText.ExamUpdated(notice, exam, now));
            batch.Add(notice.Assignment.StudentId, notice.Assignment.ToUpdatedEvent(exam));
        }

        await _unitOfWork.Notifications.AddRangeAsync(notifications, cancellationToken);
        return batch;
    }

    /// <inheritdoc />
    public async Task CollectUnreadCountsAsync(ScheduleNoticeBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        foreach (var studentId in batch.Events.Select(item => item.StudentId).Distinct())
        {
            batch.SetUnreadCount(studentId, await _unitOfWork.Notifications.CountUnreadAsync(studentId, cancellationToken));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Mỗi em một payload mang khung giờ của chính em: hai em cùng một đề không còn chung giờ từ khi có lịch
    /// riêng theo lớp. Cài đặt realtime tự nuốt lỗi, nên không có gì ở đây làm hỏng lần đổi giờ đã commit.
    /// </remarks>
    public async Task PublishAsync(ScheduleNoticeBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        foreach (var (studentId, payload) in batch.Events)
        {
            await _realtime.ExamUpdatedAsync(studentId, payload, cancellationToken);
        }

        foreach (var (studentId, unreadCount) in batch.UnreadCounts)
        {
            await _realtime.UnreadCountChangedAsync(studentId, unreadCount, cancellationToken);
        }
    }
}
