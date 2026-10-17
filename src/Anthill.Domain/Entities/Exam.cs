using Anthill.Domain.Common;

namespace Anthill.Domain.Entities;

/// <summary>
/// Bài kiểm tra. Có hai cách hình thành, phân biệt bằng <see cref="UsesQuestionList"/>:
/// <list type="bullet">
/// <item><b>Theo ma trận</b> (đề cũ): lưu số câu và tỷ lệ ba mức độ, không chứa câu hỏi cụ thể — câu hỏi
/// chỉ xuất hiện khi sinh <see cref="ExamPaper"/>.</item>
/// <item><b>Theo danh sách câu hỏi</b>: giáo viên chọn từng câu và đặt điểm cho từng câu
/// (<see cref="Questions"/>), duyệt xong mới lưu. Các cột ma trận khi đó chỉ là số liệu dẫn xuất từ danh
/// sách để màn hình cũ vẫn đọc được.</item>
/// </list>
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

    /// <summary>
    /// Đề được soạn bằng danh sách câu hỏi cụ thể (<see cref="Questions"/>) thay vì ma trận. Đặt một lần lúc
    /// tạo và chỉ đổi khi giáo viên lưu một danh sách lên đề cũ chưa ai làm.
    /// </summary>
    public bool UsesQuestionList { get; set; }

    /// <summary>
    /// Tổng điểm của danh sách câu hỏi, chụp lúc lưu. Chỉ có nghĩa khi <see cref="UsesQuestionList"/>.
    /// Lưu cứng thay vì cộng lại lúc đọc để mọi chỗ cần "tổng điểm của đề" (danh sách, màn làm bài) không
    /// phải nạp cả danh sách câu.
    /// </summary>
    public decimal ListTotalScore { get; set; }

    public bool ShuffleQuestions { get; set; }

    public bool ShuffleAnswers { get; set; }

    /// <summary>
    /// Bật: mỗi học sinh một <see cref="ExamPaper"/> riêng, seed theo assignmentId.
    /// Tắt: một đề chung duy nhất cho cả lớp, seed theo examId.
    /// </summary>
    public bool RandomizePerStudent { get; set; }

    /// <summary>
    /// Số đề nhỏ mà "đề lớn" (danh sách câu hỏi) được chia ra cho mỗi lớp. 1 = một bản đề chung như trước.
    /// Từ 2 trở lên chỉ có với đề theo danh sách: khi đó <see cref="TotalQuestions"/> và ba tỷ lệ là cấu hình của
    /// <b>một đề nhỏ</b> (số câu mỗi đề, tỷ lệ Dễ/Trung bình/Khó), không còn là số liệu dẫn xuất từ cả danh sách.
    /// </summary>
    public int PaperCount { get; set; } = 1;

    /// <summary>
    /// Số đơn vị 0,01 điểm cộng thêm cho các câu mức cao nhất của <b>mỗi</b> đề nhỏ, để đề nhỏ cộng đúng 10 khi điểm theo mức
    /// (hai chữ số thập phân, cùng mức cùng điểm) không chia ra đúng. 0 với đề một đề. Xem <c>PaperBonus</c>.
    /// </summary>
    public int PaperBonusUnits { get; set; }

    /// <summary>
    /// Mỗi lượt giao có bản đề riêng (<c>ExamPaper.AssignmentId</c> khác NULL) hay cả đề dùng một bản chung. Bật
    /// <see cref="RandomizePerStudent"/> (mỗi em bốc một đề từ ngân hàng) hoặc chia thành nhiều đề
    /// (<see cref="PaperCount"/> &gt; 1, bản của mỗi em chép từ đề nhỏ em được phát) đều cho bản đề riêng.
    /// </summary>
    public bool UsesPerAssignmentPapers => RandomizePerStudent || PaperCount > 1;

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

    /// <summary>Danh sách câu hỏi của đề, rỗng với đề theo ma trận.</summary>
    public ICollection<ExamQuestion> Questions { get; set; } = new List<ExamQuestion>();

    public ICollection<ExamAssignment> Assignments { get; set; } = new List<ExamAssignment>();

    public ICollection<ExamPaper> Papers { get; set; } = new List<ExamPaper>();
}
