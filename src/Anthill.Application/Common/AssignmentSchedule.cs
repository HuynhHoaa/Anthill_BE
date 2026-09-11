using Anthill.Application.Mappings;
using Anthill.Domain.Entities;

namespace Anthill.Application.Common;

/// <summary>
/// Lịch có hiệu lực của một bản ghi giao bài.
/// </summary>
/// <remarks>
/// <para>
/// Một đề thường được dùng cho nhiều lớp mà mỗi lớp học vào một tiết khác nhau, nên lịch của đề
/// chỉ là <b>mặc định</b>; từng lượt giao có thể đặt giờ riêng. Kể từ khi có điều đó, đọc thẳng
/// <c>exam.OpenAt</c> ở phía học sinh là sai — và sai theo cách tệ nhất: em vào được bài ngoài giờ
/// lớp mình, hoặc bị chặn trong chính giờ của mình.
/// </para>
/// <para>
/// Vì vậy mọi nơi tính giờ cho học sinh phải đi qua đây. Đây là một hàm thuần, không chạm database,
/// nên kiểm được bằng unit test — và nó được kiểm, vì đây là loại lỗi chỉ lộ ra đúng lúc thi.
/// </para>
/// </remarks>
public static class AssignmentSchedule
{
    /// <summary>Kết quả áp một khung giờ mới lên các bản ghi đã có.</summary>
    /// <param name="Updated">Số bản ghi thật sự đổi giờ.</param>
    /// <param name="KeptStarted">Số bản ghi giữ khung cũ vì em đã bắt đầu làm bài.</param>
    public readonly record struct RescheduleOutcome(int Updated, int KeptStarted);

    /// <summary>Giờ mở và giờ đóng thật sự áp cho bản ghi này.</summary>
    /// <exception cref="InvalidOperationException">
    /// Ném khi <c>Exam</c> chưa được nạp và bản ghi không có đủ cặp giờ riêng — im lặng trả về
    /// <c>default(DateTime)</c> ở đây sẽ thành năm 0001, tức mọi bài đều "đã đóng từ lâu".
    /// </exception>
    public static (DateTime OpenAt, DateTime CloseAt) Resolve(ExamAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        // Hai trường đi cùng cặp: chỉ khi có ĐỦ cả hai mới là giờ riêng. Đặt một nửa là trạng thái vô
        // nghĩa — validator chặn ở cửa vào, còn database chặn bằng ràng buộc CHECK — và nếu nó vẫn
        // lọt tới đây thì bỏ luôn nửa đã đặt, lấy trọn lịch chung, thay vì ghép nửa nọ với nửa kia
        // thành một khung giờ không ai có ý định đặt ra. Dùng chung HasOverride để hai hàm không bao
        // giờ nói hai điều khác nhau.
        if (HasOverride(assignment))
        {
            return (assignment.OpenAt!.Value, assignment.CloseAt!.Value);
        }

        var exam = RequireExam(assignment);

        return (exam.OpenAt, exam.CloseAt);
    }

    /// <summary>Như <see cref="Resolve(ExamAssignment)"/>, nhưng đề được đưa vào tường minh.</summary>
    /// <remarks>
    /// Dành cho bản ghi vừa tạo trong chính request: navigation <c>Exam</c> có được gắn hay không tuỳ
    /// vào việc EF có đang theo dõi đề — một chi tiết của repository. Bước bắn sự kiện chạy SAU commit,
    /// nên dựa vào navigation ở đó thì một lần đổi sang AsNoTracking là đủ để một lô giao bài đã ghi
    /// xong trả về lỗi 500.
    /// </remarks>
    /// <exception cref="ArgumentException">Đề đưa vào không phải đề của bản ghi này.</exception>
    public static (DateTime OpenAt, DateTime CloseAt) Resolve(ExamAssignment assignment, Exam exam)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(exam);

        if (assignment.ExamId != exam.Id)
        {
            throw new ArgumentException("Đề đưa vào không phải đề của bản ghi giao bài này.", nameof(exam));
        }

        return HasOverride(assignment)
            ? (assignment.OpenAt!.Value, assignment.CloseAt!.Value)
            : (exam.OpenAt, exam.CloseAt);
    }

    /// <summary>Bản ghi này có đặt giờ riêng hay đang theo lịch chung của đề.</summary>
    /// <remarks>Đúng khi và chỉ khi <see cref="Resolve(ExamAssignment)"/> trả về giờ riêng.</remarks>
    public static bool HasOverride(ExamAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        return assignment.OpenAt.HasValue && assignment.CloseAt.HasValue;
    }

    /// <summary>Đề của bản ghi giao bài, hoặc lỗi rõ ràng khi nó chưa được nạp.</summary>
    /// <remarks>
    /// Dự án không bật lazy loading, nên quên <c>Include(Exam)</c> thì navigation này là null. Để nó
    /// bật ra thành <see cref="NullReferenceException"/> giữa một phép so giờ là một lỗi 500 không chỉ
    /// về đâu cả; ném ở đây thì thông báo nói thẳng nguyên nhân là thiếu dữ liệu nạp chứ không phải
    /// lỗi nghiệp vụ.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Exam chưa được nạp.</exception>
    public static Exam RequireExam(ExamAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        return assignment.Exam ?? throw new InvalidOperationException(
            "Bản ghi giao bài chưa được nạp kèm Exam — không biết đề còn là nháp hay không, cũng không tính được lịch.");
    }

    /// <summary>Khung giờ này có chứa trọn thời lượng làm bài hay không.</summary>
    /// <remarks>
    /// Cùng quy tắc với lịch chung của đề (SaveExamRequestValidator, ExamService.UpdateScheduleAsync,
    /// PublishAsync), áp cho khung giờ riêng của từng lớp. Không chặn thì AttemptDeadline kẹp hạn nộp
    /// vào giờ đóng và cả lớp bị cắt bài giữa chừng mà không ai được báo. Bằng nhau vẫn là vừa.
    /// </remarks>
    public static bool FitsDuration(DateTime openAt, DateTime closeAt, int durationMinutes) =>
        durationMinutes <= (closeAt.ToUtc() - openAt.ToUtc()).TotalMinutes;

    /// <summary>
    /// Đặt khung giờ mới cho những bản ghi CHƯA có lượt làm bài; bản ghi đã bắt đầu thì giữ nguyên.
    /// </summary>
    /// <remarks>
    /// Em đã bấm "Vào thi" thì hạn nộp đã chốt vào ExpiresAt theo khung cũ; dời khung lúc này chỉ làm
    /// thẻ bài và đồng hồ nói hai giờ khác nhau. Bản ghi đã đúng khung thì không đếm, để con số trả về
    /// là số em thật sự đổi giờ chứ không phải số em được nhắc tới.
    /// </remarks>
    public static RescheduleOutcome Reschedule(
        IEnumerable<ExamAssignment> assignments,
        DateTime openAt,
        DateTime closeAt)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        var open = openAt.ToUtc();
        var close = closeAt.ToUtc();
        var updated = 0;
        var kept = 0;

        foreach (var assignment in assignments)
        {
            if (assignment.Attempt is not null)
            {
                kept++;
                continue;
            }

            // So theo thời điểm đã quy về UTC: cùng một khoảnh khắc gửi lên dưới Kind khác nhau
            // không được tính là "đổi giờ".
            if (assignment.OpenAt?.ToUtc() == open && assignment.CloseAt?.ToUtc() == close)
            {
                continue;
            }

            assignment.OpenAt = open;
            assignment.CloseAt = close;
            updated++;
        }

        return new RescheduleOutcome(updated, kept);
    }
}
