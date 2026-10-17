using Anthill.Application.Common;
using Anthill.Domain.Entities;

namespace Anthill.Application.Interfaces.Services;

/// <summary>
/// Báo cho học sinh khi giờ làm bài của các em đổi — dùng chung cho mọi đường đổi giờ.
/// </summary>
/// <remarks>
/// <para>
/// Có ba đường làm dời giờ của một em: sửa lịch chung của đề (PATCH schedule), sửa cả đề (PUT), và gửi lại một
/// lớp kèm khung giờ. Mỗi đường tự dựng thông báo thì sớm muộn một đường quên báo, hay báo cho cả những em
/// giờ không đổi. Gom về đây để cả ba đi qua một quy tắc.
/// </para>
/// <para>
/// Chia ba bước theo đúng ràng buộc của <see cref="Realtime.IRealtimeNotificationService"/>: bản ghi thông báo
/// vào cùng transaction với lần đổi giờ (<see cref="StageExamUpdatedAsync"/>), số chưa đọc đọc trong transaction
/// sau khi lưu (<see cref="CollectUnreadCountsAsync"/>), và sự kiện chỉ bắn sau commit (<see cref="PublishAsync"/>).
/// </para>
/// </remarks>
public interface IExamScheduleNotifier
{
    /// <summary>
    /// Thêm (chưa lưu) một thông báo <c>ExamUpdated</c> cho mỗi em trong danh sách còn đang theo học.
    /// </summary>
    /// <remarks>
    /// Em đang chờ xoá hoặc đã xoá bị bỏ qua — cùng tinh thần với bước giao bài bỏ qua tài khoản đã rời
    /// trường. Em đang bị khoá vẫn được báo: mở khoá xong em cần thấy đúng giờ mới.
    /// </remarks>
    Task<ScheduleNoticeBatch> StageExamUpdatedAsync(
        Exam exam,
        IEnumerable<ScheduleNotice> notices,
        DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>Đọc số chưa đọc của người nhận. Gọi sau khi đã lưu, vẫn bên trong transaction.</summary>
    Task CollectUnreadCountsAsync(ScheduleNoticeBatch batch, CancellationToken cancellationToken = default);

    /// <summary>Bắn <c>ExamUpdated</c> và <c>UnreadCountChanged</c> cho từng em. Chỉ gọi sau commit.</summary>
    Task PublishAsync(ScheduleNoticeBatch batch, CancellationToken cancellationToken = default);
}
