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
    /// <param name="Changes">
    /// Đúng những bản ghi vừa đổi, kèm cặp giờ thô trước khi đổi — để người gọi báo cho đúng những em
    /// có giờ thật sự dịch đi, không phải cho mọi em được nhắc tới.
    /// </param>
    public readonly record struct RescheduleOutcome(
        int Updated,
        int KeptStarted,
        IReadOnlyList<RescheduleChange> Changes)
    {
        /// <summary>Không có bản ghi nào để đổi.</summary>
        public static RescheduleOutcome None => new(0, 0, []);
    }

    /// <summary>Một bản ghi vừa đổi giờ, kèm cặp giờ thô (NULL = theo lịch đề) trước lần đổi.</summary>
    public sealed record RescheduleChange(ExamAssignment Assignment, DateTime? PreviousOpenAt, DateTime? PreviousCloseAt);

    /// <summary>Một nhóm bản ghi cùng cặp giờ thô, dùng để tìm khung giờ đại diện của một lớp.</summary>
    /// <param name="OpenAt">Giờ mở riêng; thiếu một trong hai mốc nghĩa là theo lịch đề.</param>
    /// <param name="CloseAt">Giờ đóng riêng.</param>
    /// <param name="Started">Nhóm này là những em đã bắt đầu làm bài.</param>
    /// <param name="Count">Số bản ghi trong nhóm.</param>
    /// <param name="LatestAssignedAt">Lần giao gần nhất trong nhóm — chỉ dùng để phân xử khi hoà.</param>
    public readonly record struct WindowSample(
        DateTime? OpenAt,
        DateTime? CloseAt,
        bool Started,
        int Count,
        DateTime LatestAssignedAt);

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

    /// <summary>Còn chừng này phút nữa là tới giờ mở thì khung giờ đó không đổi được nữa.</summary>
    public const int ScheduleLockLeadMinutes = 120;

    /// <summary>
    /// Khung giờ này đang bị khoá: từ lúc còn dưới <see cref="ScheduleLockLeadMinutes"/> phút nữa là mở
    /// cho tới hết giờ đóng.
    /// </summary>
    /// <remarks>
    /// Khoảng này là lúc học sinh đã được báo giờ và có thể đang làm bài, nên dời giờ chỉ gây hại. Còn
    /// đúng 120 phút vẫn đổi được. Hết giờ đóng thì mở khoá lại: lúc đó không ai đang làm dở, và đây là
    /// đường cứu em lỡ giờ hoặc mở lại đề. Bằng giờ đóng vẫn còn khoá, cùng cách
    /// <c>ResolveWindowLifecycle</c> coi đó là còn mở.
    /// </remarks>
    public static bool IsScheduleLocked(DateTime openAt, DateTime closeAt, DateTime now)
    {
        var moment = now.ToUtc();

        return openAt.ToUtc() - moment < TimeSpan.FromMinutes(ScheduleLockLeadMinutes)
               && moment <= closeAt.ToUtc();
    }

    /// <summary>
    /// Lịch chung của đề có đang bị khoá không. Chỉ khoá khi đề đã xuất bản và đã giao cho ai đó: đề chưa
    /// giao thì chưa em nào bị ảnh hưởng, nên giáo viên vẫn xếp lại lịch thoải mái.
    /// </summary>
    public static bool IsExamScheduleLocked(Exam exam, int assignedCount, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(exam);

        return !exam.IsDraft && assignedCount > 0 && IsScheduleLocked(exam.OpenAt, exam.CloseAt, now);
    }

    /// <summary>Request có đưa lịch chung của đề sang một cặp giờ khác không.</summary>
    public static bool ExamWindowChanges(Exam exam, DateTime openAt, DateTime closeAt)
    {
        ArgumentNullException.ThrowIfNull(exam);

        return exam.OpenAt.ToUtc() != openAt.ToUtc() || exam.CloseAt.ToUtc() != closeAt.ToUtc();
    }

    /// <summary>Cặp giờ thô sẽ nằm trên bản ghi sau khi áp một yêu cầu: thiếu một nửa thì cả cặp là NULL.</summary>
    private static (DateTime? OpenAt, DateTime? CloseAt) NormalizeWindow(DateTime? openAt, DateTime? closeAt) =>
        openAt is null || closeAt is null ? (null, null) : (openAt.ToUtc(), closeAt.ToUtc());

    /// <summary>Áp cặp giờ này lên bản ghi thì có đổi gì không, tính theo đúng phép so của <see cref="Reschedule"/>.</summary>
    public static bool WindowWouldChange(ExamAssignment assignment, DateTime? openAt, DateTime? closeAt)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var (open, close) = NormalizeWindow(openAt, closeAt);

        return assignment.OpenAt?.ToUtc() != open || assignment.CloseAt?.ToUtc() != close;
    }

    /// <summary>
    /// Đặt khung giờ mới cho những bản ghi CHƯA có lượt làm bài; bản ghi đã bắt đầu thì giữ nguyên.
    /// </summary>
    /// <remarks>
    /// Em đã bấm "Vào thi" thì hạn nộp đã chốt vào ExpiresAt theo khung cũ; dời khung lúc này chỉ làm
    /// thẻ bài và đồng hồ nói hai giờ khác nhau. Bản ghi đã đúng khung thì không đếm, để con số trả về
    /// là số em thật sự đổi giờ chứ không phải số em được nhắc tới.
    ///
    /// Cặp NULL nghĩa là đưa bản ghi về lịch chung của đề — đúng nghĩa NULL mà <see cref="Resolve(ExamAssignment)"/>
    /// đọc. Thiếu một nửa thì cả cặp được coi là NULL, như mọi chỗ khác đọc hai cột này.
    /// </remarks>
    public static RescheduleOutcome Reschedule(
        IEnumerable<ExamAssignment> assignments,
        DateTime? openAt,
        DateTime? closeAt)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        var (open, close) = NormalizeWindow(openAt, closeAt);
        var kept = 0;
        var changes = new List<RescheduleChange>();

        foreach (var assignment in assignments)
        {
            // So theo thời điểm đã quy về UTC: cùng một khoảnh khắc gửi lên dưới Kind khác nhau
            // không được tính là "đổi giờ". Bản ghi lỡ mang một nửa cặp thì vẫn bị dọn về cặp đích.
            if (!WindowWouldChange(assignment, openAt, closeAt))
            {
                continue;
            }

            // Chỉ đếm em đã bắt đầu khi khung giờ yêu cầu thật sự khác khung em đang giữ: gửi lại một lớp
            // với đúng khung cũ mà vẫn báo "3 em giữ khung giờ cũ" là bắt giáo viên đi tìm một khác biệt
            // không tồn tại.
            if (assignment.Attempt is not null)
            {
                kept++;
                continue;
            }

            changes.Add(new RescheduleChange(assignment, assignment.OpenAt, assignment.CloseAt));
            assignment.OpenAt = open;
            assignment.CloseAt = close;
        }

        return new RescheduleOutcome(changes.Count, kept, changes);
    }

    /// <summary>Khung giờ có hiệu lực của bản ghi đã khác đi so với cặp giờ thô trước đó hay chưa.</summary>
    /// <remarks>
    /// Đổi cột không đồng nghĩa với đổi giờ: đặt giờ riêng trùng khít lịch đề, hay gỡ giờ riêng vốn trùng
    /// lịch đề, đều không dời em đi phút nào — báo "lịch đã đổi" trong trường hợp đó là tập cho em quen bỏ
    /// qua loại thông báo này.
    /// </remarks>
    public static bool EffectiveWindowChanged(
        ExamAssignment assignment,
        Exam exam,
        DateTime? previousOpenAt,
        DateTime? previousCloseAt)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(exam);

        var (beforeOpen, beforeClose) = previousOpenAt.HasValue && previousCloseAt.HasValue
            ? (previousOpenAt.Value, previousCloseAt.Value)
            : (exam.OpenAt, exam.CloseAt);
        var (openAt, closeAt) = Resolve(assignment, exam);

        return beforeOpen.ToUtc() != openAt.ToUtc() || beforeClose.ToUtc() != closeAt.ToUtc();
    }

    /// <summary>Ảnh chụp lịch chung và thời lượng của đề trước một lần sửa.</summary>
    public readonly record struct ExamTiming(DateTime OpenAt, DateTime CloseAt, int DurationMinutes)
    {
        public static ExamTiming Of(Exam exam)
        {
            ArgumentNullException.ThrowIfNull(exam);
            return new ExamTiming(exam.OpenAt.ToUtc(), exam.CloseAt.ToUtc(), exam.DurationMinutes);
        }
    }

    /// <summary>Một lần sửa đề đổi gì với một em chưa bắt đầu: khung giờ, thời lượng, hay không gì cả.</summary>
    /// <param name="assignment">Bản ghi của em — người gọi chỉ đưa vào em CHƯA bắt đầu làm bài.</param>
    /// <param name="before">Lịch chung và thời lượng trước lần sửa.</param>
    /// <param name="after">Đề sau lần sửa.</param>
    /// <param name="now">Giờ server của lần sửa, để bỏ qua những em mà giờ làm bài đã qua từ trước.</param>
    /// <remarks>
    /// Em có giờ riêng không bị lịch chung kéo theo, nên đổi lịch chung không phải chuyện của em — nhưng đổi
    /// thời lượng thì em nào cũng làm bài theo con số mới. Đề đang là nháp thì không em nào vào được, nên
    /// không có gì để báo. Em đã bắt đầu không nằm ở đây: hạn nộp của em đã chốt lúc vào thi.
    /// </remarks>
    public static (bool WindowChanged, bool DurationChanged) ChangesFromExamEdit(
        ExamAssignment assignment,
        ExamTiming before,
        Exam after,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(after);

        if (after.IsDraft)
        {
            return (false, false);
        }

        var hasOwnWindow = HasOverride(assignment);
        var (_, closeAt) = Resolve(assignment, after);
        var closeBefore = hasOwnWindow ? closeAt : before.CloseAt;

        var durationChanged = before.DurationMinutes != after.DurationMinutes
                              && StillMatters(closeBefore, closeAt, now);

        var windowChanged = !hasOwnWindow
                            && (before.OpenAt.ToUtc() != after.OpenAt.ToUtc()
                                || before.CloseAt.ToUtc() != after.CloseAt.ToUtc())
                            && StillMatters(closeBefore, closeAt, now);

        return (windowChanged, durationChanged);
    }

    /// <summary>Lần đổi này còn thay đổi được điều gì cho em, hay cả hai khung giờ đều đã qua.</summary>
    /// <remarks>
    /// Sửa lịch một đề của tháng trước — vì gõ nhầm, hay để dọn dẹp — không được đánh thức thông báo cho cả
    /// trăm em mà giờ làm bài đã hết từ lâu và vẫn hết. Chỉ cần một trong hai mốc đóng còn ở phía trước thì
    /// lần đổi có hệ quả thật: hoặc em vừa mất giờ làm bài, hoặc em vừa được mở lại.
    /// </remarks>
    public static bool StillMatters(DateTime previousCloseAt, DateTime newCloseAt, DateTime now)
    {
        var moment = now.ToUtc();
        return previousCloseAt.ToUtc() > moment || newCloseAt.ToUtc() > moment;
    }

    /// <summary>Cặp giờ thô cho một bản ghi giao bài sắp được tạo.</summary>
    /// <param name="viaGroupId">Lớp mà em được giao qua; NULL khi giáo viên chọn đích danh em.</param>
    /// <param name="requestWindow">Cặp giờ chung của request (NULL cả hai = không kèm giờ).</param>
    /// <param name="classWindows">Khung giờ riêng của từng lớp trong request, NULL cả hai = theo lịch đề.</param>
    /// <param name="inheritedWindows">Khung giờ đại diện của các lớp đã có bài từ trước.</param>
    /// <remarks>
    /// Thứ tự ưu tiên: khung giờ khai riêng cho lớp → cặp giờ chung của request → khung giờ lớp đang theo
    /// → lịch đề. Vế thứ ba là để em mới xếp vào lớp làm bài cùng giờ với cả lớp khi giáo viên chỉ gửi lại
    /// lớp để giao bổ sung. Em chọn đích danh không có lớp nào để kế thừa, nên chỉ theo request.
    /// </remarks>
    public static (DateTime? OpenAt, DateTime? CloseAt) WindowForNewAssignment(
        int? viaGroupId,
        (DateTime? OpenAt, DateTime? CloseAt) requestWindow,
        IReadOnlyDictionary<int, (DateTime? OpenAt, DateTime? CloseAt)> classWindows,
        IReadOnlyDictionary<int, (DateTime? OpenAt, DateTime? CloseAt)?> inheritedWindows)
    {
        ArgumentNullException.ThrowIfNull(classWindows);
        ArgumentNullException.ThrowIfNull(inheritedWindows);

        var requestHasWindow = requestWindow.OpenAt.HasValue && requestWindow.CloseAt.HasValue;

        if (viaGroupId is not { } groupId)
        {
            return requestHasWindow ? requestWindow : (null, null);
        }

        if (classWindows.TryGetValue(groupId, out var classWindow))
        {
            return classWindow.OpenAt.HasValue && classWindow.CloseAt.HasValue ? classWindow : (null, null);
        }

        if (requestHasWindow)
        {
            return requestWindow;
        }

        return inheritedWindows.TryGetValue(groupId, out var inherited) && inherited is { } window
            ? window
            : (null, null);
    }

    /// <summary>Khung giờ đại diện cho một lớp: cặp giờ thô mà các em của lớp đó đang theo.</summary>
    /// <remarks>
    /// <para>
    /// Các em chưa bắt đầu là nguồn đúng: gửi lại lớp kèm giờ mới chỉ dời những em này, còn em đã vào thi
    /// giữ khung của lúc đó. Chỉ khi cả lớp đã bắt đầu mới xét trên toàn lớp. Cặp giờ có nhiều em nhất
    /// thắng; hoà thì lấy cặp của lần giao gần nhất. Trong vận hành bình thường các em chưa bắt đầu luôn
    /// cùng một cặp, nên phần phân xử chỉ có nghĩa với dữ liệu cũ.
    /// </para>
    /// <para>
    /// Trả về <c>null</c> khi không có mẫu nào. Cặp NULL bên trong một kết quả khác <c>null</c> nghĩa là
    /// lớp đang theo lịch chung của đề.
    /// </para>
    /// </remarks>
    public static (DateTime? OpenAt, DateTime? CloseAt)? RepresentativeWindow(IEnumerable<WindowSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var all = samples.Where(sample => sample.Count > 0).ToList();
        if (all.Count == 0)
        {
            return null;
        }

        var pool = all.Any(sample => !sample.Started)
            ? all.Where(sample => !sample.Started).ToList()
            : all;

        var winner = pool
            .GroupBy(sample => sample.OpenAt.HasValue && sample.CloseAt.HasValue
                ? (OpenAt: sample.OpenAt.ToUtc(), CloseAt: sample.CloseAt.ToUtc())
                : (OpenAt: (DateTime?)null, CloseAt: (DateTime?)null))
            .Select(group => new
            {
                Window = group.Key,
                Count = group.Sum(sample => sample.Count),
                Latest = group.Max(sample => sample.LatestAssignedAt.ToUtc())
            })
            .OrderByDescending(group => group.Count)
            .ThenByDescending(group => group.Latest)
            .First();

        return winner.Window;
    }
}
