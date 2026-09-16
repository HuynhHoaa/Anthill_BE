using Anthill.Application.Common;

namespace Anthill.Application.Interfaces.Services;

/// <summary>
/// Nhắc học sinh chưa bắt đầu làm bài rằng giờ của em sắp hết (§7, sự kiện <c>DeadlineSoon</c>).
/// </summary>
/// <remarks>
/// <para>
/// Việc này <b>phải</b> do tiến trình nền làm, không phải do trình duyệt bấm giờ: em không mở ứng dụng thì
/// không có ai để bấm, mà đúng em đó mới là người cần được nhắc.
/// </para>
/// <para>
/// Mỗi khung giờ một lời nhắc, và mốc đã nhắc nằm trong database (<c>ExamAssignment.DeadlineNoticeCloseAt</c>)
/// nên khởi động lại giữa buổi thi không nhắc lại từ đầu. Giáo viên dời khung sang giờ khác thì em được nhắc
/// lại theo giờ mới, vì mốc đã nhắc không còn khớp.
/// </para>
/// </remarks>
public interface IDeadlineReminderService
{
    /// <summary>Một lượt quét: gửi lời nhắc cho những em đã tới lúc, trả về số em được nhắc.</summary>
    /// <param name="batchSize">Trần số em xử lý trong một lượt; phần dư để vòng sau.</param>
    /// <param name="marginMinutes">Biên cộng vào thời lượng làm bài để ra mốc nhắc.</param>
    Task<Result<int>> SendDueRemindersAsync(
        int batchSize,
        int marginMinutes,
        CancellationToken cancellationToken = default);
}
