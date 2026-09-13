using Anthill.Application.Common;
using Anthill.Application.DTOs.ExamAssignments;

namespace Anthill.Application.Interfaces.Services;

/// <summary>Giao bài và thu hồi — §4.6 phần assignment.</summary>
/// <remarks>
/// Ràng buộc đã cài (§5):
/// <list type="bullet">
/// <item><b>Khử trùng lặp ở backend</b>: <c>targets = distinct(studentIds hợp lệ ∪ học sinh của
/// groupIds)</c>. Con số frontend tính chỉ để hiện lên màn hình.</item>
/// <item><b>Idempotent</b> theo cặp <c>(examId, studentId)</c>. Giao lại cho người đã có bài thì
/// bỏ qua, giữ nguyên trạng thái, lượt làm bài và điểm.</item>
/// <item><b>Kiểm phạm vi:</b> học sinh phải thuộc nhóm do chính giáo viên đó quản lý.</item>
/// <item><b>Học sinh bị khoá thì bỏ qua</b> và báo lại số lượng.</item>
/// <item><b>Chặn giao đề đang là nháp</b> — frontend hoàn toàn không kiểm điều này.</item>
/// <item>Toàn bộ nằm trong một transaction: assignment + <c>ExamPaper</c> + thông báo.</item>
/// <item><see cref="RevokeAsync"/> từ chối khi học sinh đã bắt đầu làm bài.</item>
/// </list>
/// </remarks>
public interface IExamAssignmentService
{
    /// <summary>Danh sách học sinh đã giao, kèm tiến trình làm bài của từng em.</summary>
    Task<Result<IReadOnlyList<ExamAssignmentDto>>> ListAsync(
        int examId,
        CancellationToken cancellationToken = default);

    /// <summary>Giao bài cho học sinh lẻ và/hoặc cả nhóm. Idempotent.</summary>
    Task<Result<AssignExamResponse>> AssignAsync(
        int examId,
        AssignExamRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dời giờ cho một em được chọn đích danh, hoặc đưa em về lịch chung của đề.
    /// </summary>
    /// <remarks>
    /// Từ chối (409) với em được giao qua lớp — giờ của em đó là giờ của lớp, sửa bằng cách gửi lại lớp — và
    /// với em đã bắt đầu làm bài, vì hạn nộp đã chốt lúc em vào thi.
    /// </remarks>
    Task<Result<ExamAssignmentDto>> RescheduleAsync(
        int examId,
        int assignmentId,
        UpdateAssignmentScheduleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thu hồi một bài đã giao. Bổ sung so với frontend: giao nhầm lớp là chuyện thường, mà
    /// endpoint giao bài lại cố ý idempotent nên không có đường lùi.
    /// </summary>
    Task<Result> RevokeAsync(int examId, int assignmentId, CancellationToken cancellationToken = default);
}
