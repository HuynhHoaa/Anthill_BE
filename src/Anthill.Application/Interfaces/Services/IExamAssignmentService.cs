using Anthill.Application.Common;
using Anthill.Application.DTOs.ExamAssignments;

namespace Anthill.Application.Interfaces.Services;

/// <summary>Giao bài theo lớp và thu hồi theo lớp — §4.6 phần assignment.</summary>
/// <remarks>
/// Ràng buộc đã cài (§5):
/// <list type="bullet">
/// <item><b>Giao bài chỉ theo lớp.</b> Không có đường giao riêng cho từng học sinh: request mang danh sách
/// học sinh bị từ chối (400).</item>
/// <item><b>Khử trùng lặp ở backend</b>: <c>targets = distinct(học sinh của các lớp được chọn)</c>. Con số
/// frontend tính chỉ để hiện lên màn hình.</item>
/// <item><b>Idempotent</b> theo cặp <c>(examId, studentId)</c>. Giao lại lớp cho người đã có bài thì
/// bỏ qua, giữ nguyên trạng thái, lượt làm bài và điểm; em mới vào lớp thì nhận bài ở lần gửi lại.</item>
/// <item><b>Kiểm phạm vi:</b> lớp phải đã công bố và do chính giáo viên đó được phân công.</item>
/// <item><b>Học sinh bị khoá thì bỏ qua</b> và báo lại số lượng.</item>
/// <item><b>Chặn giao đề đang là nháp</b> — frontend hoàn toàn không kiểm điều này.</item>
/// <item>Toàn bộ nằm trong một transaction: assignment + <c>ExamPaper</c> + thông báo.</item>
/// <item><see cref="RevokeClassAsync"/> chỉ gỡ em chưa bắt đầu làm bài.</item>
/// </list>
/// </remarks>
public interface IExamAssignmentService
{
    /// <summary>Danh sách học sinh đã giao, kèm tiến trình làm bài của từng em.</summary>
    Task<Result<IReadOnlyList<ExamAssignmentDto>>> ListAsync(
        int examId,
        CancellationToken cancellationToken = default);

    /// <summary>Giao bài cho một hay nhiều lớp. Idempotent.</summary>
    Task<Result<AssignExamResponse>> AssignAsync(
        int examId,
        AssignExamRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thu hồi bài đã giao cho <b>cả một lớp</b>: gỡ mọi em của lớp đó chưa bắt đầu làm bài. Giao nhầm lớp là
    /// chuyện thường, mà endpoint giao bài lại cố ý idempotent nên không có đường lùi.
    /// </summary>
    /// <remarks>
    /// Em đã bắt đầu làm giữ nguyên bài (gỡ lúc đó là xoá luôn phần bài đã làm) và được đếm riêng ở kết quả.
    /// Trả <c>NotFound</c> khi lớp chưa được giao đề này, <c>Conflict</c> khi mọi em của lớp đã bắt đầu.
    /// </remarks>
    Task<Result<RevokeClassResponse>> RevokeClassAsync(
        int examId,
        int groupId,
        CancellationToken cancellationToken = default);
}
