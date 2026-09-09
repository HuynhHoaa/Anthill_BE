using EduQuiz.Domain.Common;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Domain.Entities;

/// <summary>
/// Một lượt làm bài. Điểm và số câu đúng do server tính từ snapshot của đề,
/// không bao giờ nhận từ client.
/// </summary>
public class ExamAttempt : BaseEntity
{
    public int AssignmentId { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>
    /// <c>StartedAt + DurationMinutes</c>, chốt ngay lúc bắt đầu. Mọi kiểm tra hạn giờ
    /// so với cột này và <c>DateTime.UtcNow</c>; đồng hồ máy học sinh không có giá trị pháp lý.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public AssignmentStatus Status { get; set; } = AssignmentStatus.InProgress;

    public decimal Score { get; set; }

    /// <summary>Tổng điểm tối đa, chốt lúc sinh đề để đề sửa về sau không đổi thang điểm cũ.</summary>
    public decimal MaxScore { get; set; }

    public int CorrectCount { get; set; }

    /// <summary>Bài do background job tự nộp khi hết giờ, không phải học sinh bấm nộp.</summary>
    public bool AutoSubmitted { get; set; }

    /// <summary>
    /// Chống autosave tới sai thứ tự: bỏ qua gói lưu nháp có <c>clientSeq</c> nhỏ hơn
    /// giá trị này. Là bộ đếm của client, không phải mốc thời gian.
    /// </summary>
    public long LastAppliedSeq { get; set; }

    public ExamAssignment Assignment { get; set; } = null!;

    public ICollection<StudentAnswer> Answers { get; set; } = new List<StudentAnswer>();
}
