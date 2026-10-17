namespace Anthill.Application.DTOs.Exams;

/// <summary>
/// Một khung giờ đang áp cho một phần người được giao đề: một lớp, hoặc (dữ liệu cũ) một nhóm học sinh giao lẻ cùng giờ.
/// </summary>
/// <remarks>
/// <para>
/// Có mặt vì lịch trên <see cref="ExamDto"/> chỉ là lịch <b>mặc định</b>. Đề giao cho 12A1 sáng thứ Ba
/// và 12A2 chiều thứ Năm mà màn hình giáo viên chỉ hiện lịch chung cả tuần thì giáo viên không trả lời
/// được câu hỏi đơn giản nhất: "lớp nào đang làm bài lúc này?".
/// </para>
/// <para>
/// Server tính sẵn giờ đã phân giải và <see cref="Lifecycle"/> theo cùng một phép so với phía học sinh,
/// để giao diện không phải ghép lại cột NULL với lịch đề — ghép sai ở đây là báo "đang mở" cho một lớp
/// mà học sinh bị chặn ngoài cửa.
/// </para>
/// </remarks>
public sealed record AssignmentWindowDto
{
    /// <summary>Lớp mà các em được giao qua. NULL (vắng khỏi JSON) chỉ với dữ liệu cũ, từ thời còn giao riêng từng em.</summary>
    public int? GroupId { get; init; }

    /// <summary>Tên lớp; NULL cùng <see cref="GroupId"/>.</summary>
    public string? GroupName { get; init; }

    /// <summary>Giờ mở có hiệu lực — giờ riêng nếu có, ngược lại là lịch chung của đề.</summary>
    public required DateTime OpenAt { get; init; }

    public required DateTime CloseAt { get; init; }

    /// <summary>false khi khung giờ này là lịch chung của đề — sửa lịch đề sẽ kéo nó theo.</summary>
    public required bool HasScheduleOverride { get; init; }

    /// <summary><c>"draft"</c> · <c>"scheduled"</c> · <c>"open"</c> · <c>"closed"</c>, tính theo giờ server lúc đọc.</summary>
    public required string Lifecycle { get; init; }

    /// <summary>
    /// <c>true</c> khi khung giờ này không đổi được nữa (còn dưới 120 phút nữa là mở, hoặc đang mở).
    /// </summary>
    public required bool ScheduleLocked { get; init; }

    /// <summary>Số em thuộc phần này. Với một lớp là cả lớp, kể cả em đã bắt đầu theo khung cũ.</summary>
    public required int AssignedCount { get; init; }

    /// <summary>Số em trong phần này đã có lượt làm bài.</summary>
    public required int StartedCount { get; init; }
}
