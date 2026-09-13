namespace Anthill.Application.DTOs.ExamAssignments;

/// <summary>Thân request của <c>PATCH /api/exams/{examId}/assignments/{assignmentId}/schedule</c>.</summary>
/// <remarks>
/// <para>
/// Dời giờ cho <b>một em được chọn đích danh</b>. Gửi lại lớp là cách dời giờ cho em được giao qua lớp,
/// nhưng em chọn lẻ không có lớp nào để gửi lại — trước khi có endpoint này, giao nhầm giờ cho một em
/// học bù thì chỉ còn cách thu hồi rồi giao lại, và cách đó không dùng được nữa khi em đã có bản đề.
/// </para>
/// <para>
/// Cùng nghĩa với hai cột trên bản ghi giao bài: đủ cả hai mốc là khung giờ riêng, bỏ trống cả hai là
/// đưa em về lịch chung của đề. Gửi một nửa bị từ chối.
/// </para>
/// </remarks>
public sealed record UpdateAssignmentScheduleRequest
{
    public DateTime? OpenAt { get; init; }

    public DateTime? CloseAt { get; init; }
}
