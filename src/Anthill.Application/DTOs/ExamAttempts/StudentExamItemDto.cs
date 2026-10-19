namespace Anthill.Application.DTOs.ExamAttempts;

/// <summary>
/// Một bài kiểm tra được giao, dưới góc nhìn học sinh — <c>GET /api/my/exams</c>.
/// </summary>
/// <remarks>
/// <para>
/// Không phải <c>ExamDto</c> rút gọn. Đề của giáo viên mang theo blueprint (tỷ lệ ba mức độ, cờ
/// xáo trộn, số bài đã nộp của cả lớp) — những thứ học sinh không có lý do gì để đọc, mà biết thì
/// đoán được cấu trúc đề.
/// </para>
/// <para>
/// Điểm số chỉ xuất hiện khi giáo viên bật <c>showResultAfterSubmit</c> (§3 quy tắc ⑥). Không đủ
/// điều kiện thì trường đó <b>vắng mặt</b> khỏi JSON, không phải ẩn ở giao diện.
/// </para>
/// </remarks>
public sealed record StudentExamItemDto
{
    /// <summary>
    /// Id bản ghi giao bài. Đây là giá trị gửi lên <c>POST /api/exam-attempts</c>, không phải
    /// <see cref="ExamId"/> — một đề giao cho bốn mươi em là bốn mươi assignment khác nhau.
    /// </summary>
    public required int AssignmentId { get; init; }

    public required int ExamId { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required string Topic { get; init; }

    public required int DurationMinutes { get; init; }

    public required int TotalQuestions { get; init; }

    /// <summary>
    /// Tổng điểm tối đa của đề (số câu mỗi mức × điểm mức đó), hiển thị ở màn giới thiệu. Thẻ bài không
    /// mang điểm từng mức. Điểm của từng câu trong bản đề (<c>AttemptQuestionDto.Point</c>) thì có chủ ý
    /// hiện lúc làm bài và ở kết quả, để học sinh biết câu nào nặng hơn.
    /// </summary>
    public required decimal TotalScore { get; init; }

    /// <summary>Đề có xáo thứ tự câu hỏi giữa các lượt làm hay không — chỉ để hiển thị, không phải blueprint.</summary>
    public required bool ShuffleQuestions { get; init; }

    /// <summary>Đề có xáo thứ tự đáp án giữa các lượt làm hay không — chỉ để hiển thị, không phải blueprint.</summary>
    public required bool ShuffleAnswers { get; init; }

    public required DateTime OpenAt { get; init; }

    public required DateTime CloseAt { get; init; }

    /// <summary>Tên giáo viên ra đề, cho màn giới thiệu trước khi vào thi.</summary>
    public required string TeacherName { get; init; }

    /// <summary><c>"draft"</c> · <c>"scheduled"</c> · <c>"open"</c> · <c>"closed"</c>, tính theo giờ server lúc đọc.</summary>
    public required string Lifecycle { get; init; }

    /// <summary>
    /// <c>"assigned"</c> · <c>"inprogress"</c> · <c>"submitted"</c> · <c>"graded"</c> · <c>"expired"</c>.
    /// </summary>
    /// <remarks>
    /// <c>expired</c> tính theo đúng khung giờ của <see cref="Lifecycle"/>, <see cref="OpenAt"/> và
    /// <see cref="CloseAt"/> — lịch riêng của lớp nếu có — nên không bao giờ có chuyện thẻ bài vừa
    /// "đang mở" vừa "hết hạn".
    /// </remarks>
    public required string Status { get; init; }

    /// <summary>
    /// Nút "Vào thi" có bấm được không.
    /// </summary>
    /// <remarks>
    /// Tính ở server chứ không để frontend tự suy từ <c>openAt</c>/<c>closeAt</c>: máy học sinh
    /// chỉnh sai giờ sẽ tự cho mình bấm sớm, rồi nhận về một lỗi khó hiểu thay vì một nút mờ.
    /// </remarks>
    public required bool CanStart { get; init; }

    /// <summary>Học sinh có được xem điểm ngay sau khi nộp hay không.</summary>
    public required bool ShowResultAfterSubmit { get; init; }

    /// <summary>NULL khi em chưa bắt đầu làm bài.</summary>
    public int? AttemptId { get; init; }

    public DateTime? StartedAt { get; init; }

    /// <summary>Hạn giờ của lượt đang làm dở, để danh sách hiện được số phút còn lại.</summary>
    public DateTime? ExpiresAt { get; init; }

    public DateTime? SubmittedAt { get; init; }

    /// <summary>Chỉ có khi bài đã chấm <b>và</b> giáo viên cho xem kết quả ngay.</summary>
    public decimal? Score { get; init; }

    /// <summary>Cùng điều kiện với <see cref="Score"/> — không có điểm thì thang điểm cũng vô nghĩa.</summary>
    public decimal? MaxScore { get; init; }

    public int? CorrectCount { get; init; }

    public required DateTime AssignedAt { get; init; }

    /// <summary>Lớp mà bài này được giao qua đó; NULL với bài giao lẻ cũ. Một em học nhiều lớp nên thẻ bài phải ghi rõ bài của lớp nào.</summary>
    public int? GroupId { get; init; }

    /// <summary>Tên lớp, đi cùng <see cref="GroupId"/>. Có thể vắng khi chỗ gọi không nạp kèm lớp (màn giới thiệu một bài).</summary>
    public string? GroupName { get; init; }
}
