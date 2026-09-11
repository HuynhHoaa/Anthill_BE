namespace Anthill.Application.DTOs.Results;

/// <summary>
/// Một dòng trong bảng kết quả — dùng chung cho màn hình giáo viên và màn hình học sinh.
/// </summary>
/// <remarks>
/// Cùng một kiểu nhưng <b>không cùng nội dung</b>: khi học sinh chưa được phép xem điểm
/// (<c>showResultAfterSubmit</c> tắt, hoặc bài chưa chấm xong), ba trường điểm ở đây để NULL và
/// biến mất khỏi JSON. Ẩn ở giao diện là không đủ — §3 quy tắc ⑥ nói điểm không được rời server.
/// </remarks>
public sealed record ResultSummaryDto
{
    public required int AttemptId { get; init; }

    public required int AssignmentId { get; init; }

    public required int ExamId { get; init; }

    public required string ExamTitle { get; init; }

    public required string Topic { get; init; }

    public required int StudentId { get; init; }

    public required string StudentName { get; init; }

    public required string StudentCode { get; init; }

    /// <summary>Tên nhóm lớp, chuỗi rỗng khi em chưa được phân nhóm.</summary>
    public required string Group { get; init; }

    /// <summary><c>submitted</c> · <c>graded</c> · <c>inprogress</c> · <c>expired</c>.</summary>
    public required string Status { get; init; }

    public required DateTime StartedAt { get; init; }

    public DateTime? SubmittedAt { get; init; }

    /// <summary>Bài có phải do hệ thống tự nộp khi hết giờ hay không.</summary>
    public required bool AutoSubmitted { get; init; }

    /// <summary>Số câu của bản đề em đó nhận. Đọc từ bản đề, không đọc cấu hình hiện tại của đề.</summary>
    public required int TotalQuestions { get; init; }

    /// <summary>NULL khi người gọi chưa được phép xem điểm.</summary>
    public decimal? Score { get; init; }

    /// <summary>NULL cùng lúc với <see cref="Score"/>.</summary>
    public decimal? MaxScore { get; init; }

    /// <summary>NULL cùng lúc với <see cref="Score"/>.</summary>
    public int? CorrectCount { get; init; }
}
