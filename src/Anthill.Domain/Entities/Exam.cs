using Anthill.Domain.Common;

namespace Anthill.Domain.Entities;

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

    /// <summary>
    /// Điểm của MỘT câu ở mức Dễ. Tổng điểm của đề là số câu mỗi mức nhân điểm mức đó; 100 là tổng chuẩn
    /// chứ không bắt buộc (xem <c>ExamScoring</c> ở tầng Application).
    /// </summary>
    public decimal PointEasy { get; set; }

    /// <summary>Điểm của MỘT câu ở mức Trung bình. Xem <see cref="PointEasy"/>.</summary>
    public decimal PointMedium { get; set; }

    /// <summary>Điểm của MỘT câu ở mức Khó. Xem <see cref="PointEasy"/>.</summary>
    public decimal PointHard { get; set; }

    /// <summary>Điểm của một câu thuộc mức độ <paramref name="difficulty"/> trong đề này.</summary>
    public decimal PointFor(Anthill.Domain.Enums.QuestionDifficulty difficulty) => difficulty switch
    {
        Anthill.Domain.Enums.QuestionDifficulty.Easy => PointEasy,
        Anthill.Domain.Enums.QuestionDifficulty.Medium => PointMedium,
        Anthill.Domain.Enums.QuestionDifficulty.Hard => PointHard,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null)
    };

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

    /// <summary>
    /// Mốc mở đáp án đã CHỐT — chỉ tiến, không lùi (H10). NULL nghĩa là chưa từng chốt. Xem
    /// <see cref="Anthill.Application.Common.AnswersReleaseStamp"/> cho lý do tồn tại của cột này:
    /// không có nó thì tính lại theo <c>MAX(CloseAt)</c> mỗi lần đọc khiến một lượt giao MỚI với giờ
    /// đóng muộn hơn giấu lại đáp án của những em đã hợp lệ xem rồi.
    /// </summary>
    public DateTime? AnswersReleasedAt { get; set; }

    /// <summary>Môn của đề suy ra từ đây (CreatedBy.SubjectId) — Exam cố ý không có SubjectId.</summary>
    public int CreatedByUserId { get; set; }

    public User CreatedBy { get; set; } = null!;

    public ICollection<ExamAssignment> Assignments { get; set; } = new List<ExamAssignment>();

    public ICollection<ExamPaper> Papers { get; set; } = new List<ExamPaper>();
}
