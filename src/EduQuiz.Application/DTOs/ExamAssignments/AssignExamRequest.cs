namespace EduQuiz.Application.DTOs.ExamAssignments;

/// <summary>Thân request của <c>POST /api/exams/{id}/assignments</c>.</summary>
/// <remarks>
/// Nhận cả học sinh lẻ lẫn cả nhóm trong một lần gọi, vì màn hình giao bài cho chọn xen kẽ hai
/// kiểu. Backend gộp và khử trùng lặp; con số frontend tự tính chỉ để hiện lên màn hình (§5).
/// </remarks>
public sealed record AssignExamRequest
{
    public IReadOnlyList<int> StudentIds { get; init; } = [];

    /// <summary>Giao cho toàn bộ học sinh của những nhóm này, tính tại thời điểm gọi.</summary>
    public IReadOnlyList<int> GroupIds { get; init; } = [];

    /// <summary>
    /// Bật: mỗi em một bản đề riêng. Tắt: cả lớp chung một bản đề.
    /// </summary>
    /// <remarks>
    /// Giá trị này được ghi lên <c>Exam</c> chứ không lên từng assignment, vì một đề mà nửa lớp
    /// dùng chung đề còn nửa kia mỗi em một đề thì không so sánh kết quả với nhau được nữa.
    /// Đổi cờ sau khi đã giao <b>không</b> sinh lại những bản đề đã phát.
    /// </remarks>
    public bool RandomizePerStudent { get; init; }

    /// <summary>Gửi thông báo cho những em vừa được giao. Chỉ áp dụng cho em <b>mới</b>, không nhắc lại.</summary>
    public bool Notify { get; init; } = true;
}
