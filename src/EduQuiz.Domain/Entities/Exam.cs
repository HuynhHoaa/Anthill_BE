using EduQuiz.Domain.Common;

namespace EduQuiz.Domain.Entities;

/// <summary>
/// Bài kiểm tra lưu dưới dạng <b>blueprint</b>, không chứa danh sách câu hỏi cụ thể —
/// câu hỏi chỉ xuất hiện khi sinh <see cref="ExamPaper"/>.
/// </summary>
public class Exam : BaseEntity
{
    public required string Title { get; set; }

    public string? Description { get; set; }

    /// <summary>Chủ đề dùng để lọc khi sinh đề. Rỗng/NULL nghĩa là lấy từ mọi chủ đề.</summary>
    public string? Topic { get; set; }

    public int DurationMinutes { get; set; }

    public int TotalQuestions { get; set; }

    public int EasyPercentage { get; set; }

    public int MediumPercentage { get; set; }

    public int HardPercentage { get; set; }

    public decimal PointPerQuestion { get; set; }

    public bool ShuffleQuestions { get; set; }

    public bool ShuffleAnswers { get; set; }

    /// <summary>
    /// Bật: mỗi học sinh một <see cref="ExamPaper"/> riêng, seed theo assignmentId.
    /// Tắt: một đề chung duy nhất cho cả lớp, seed theo examId.
    /// </summary>
    public bool RandomizePerStudent { get; set; }

    public bool ShowResultAfterSubmit { get; set; }

    public DateTime OpenAt { get; set; }

    public DateTime CloseAt { get; set; }

    /// <summary>
    /// Vòng đời chỉ lưu đúng bước nháp/xuất bản. Scheduled · Open · Closed là giá trị
    /// dẫn xuất, tính từ <see cref="OpenAt"/>/<see cref="CloseAt"/> so với giờ server lúc đọc.
    /// </summary>
    public bool IsDraft { get; set; } = true;

    public DateTime? PublishedAt { get; set; }

    /// <summary>Môn của đề suy ra từ đây (CreatedBy.SubjectId) — Exam cố ý không có SubjectId.</summary>
    public int CreatedByUserId { get; set; }

    public User CreatedBy { get; set; } = null!;

    public ICollection<ExamAssignment> Assignments { get; set; } = new List<ExamAssignment>();

    public ICollection<ExamPaper> Papers { get; set; } = new List<ExamPaper>();
}
