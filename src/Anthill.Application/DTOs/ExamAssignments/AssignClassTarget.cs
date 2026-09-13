namespace Anthill.Application.DTOs.ExamAssignments;

/// <summary>Một lớp trong request giao bài, kèm khung giờ của riêng lớp đó.</summary>
/// <remarks>
/// <para>
/// Có mặt để một lần bấm giao được cho nhiều lớp mà mỗi lớp một tiết: 12A1 sáng thứ Ba, 12A2 chiều thứ
/// Năm. Với một cặp giờ chung cho cả request thì giáo viên phải giao từng lớp một, và giữa hai lần bấm
/// là lúc lớp thứ hai dễ bị quên nhất.
/// </para>
/// <para>
/// Khác <see cref="AssignExamRequest.GroupIds"/> ở chỗ mục này nói <b>trọn</b> khung giờ của lớp: cặp
/// giờ bỏ trống nghĩa là lớp theo lịch chung của đề, và những em chưa bắt đầu đang mang giờ riêng sẽ được
/// đưa về lịch đề. <c>GroupIds</c> thì giữ nghĩa cũ — không kèm giờ là không đụng tới giờ đã đặt.
/// </para>
/// </remarks>
public sealed record AssignClassTarget
{
    public int GroupId { get; init; }

    /// <summary>Giờ mở riêng của lớp. Bỏ trống cùng <see cref="CloseAt"/> thì theo lịch chung của đề.</summary>
    public DateTime? OpenAt { get; init; }

    /// <summary>Giờ đóng riêng của lớp. Đi cùng cặp với <see cref="OpenAt"/>.</summary>
    public DateTime? CloseAt { get; init; }
}
