namespace EduQuiz.Application.DTOs.Results;

/// <summary>Chi tiết một lượt làm bài — soi từng câu.</summary>
/// <remarks>
/// Cùng một endpoint phục vụ hai vai trò với hai mức chi tiết khác nhau. Giáo viên sở hữu đề luôn
/// thấy đáp án đúng; học sinh chỉ thấy khi bài là của mình, đã chấm xong, và giáo viên bật
/// <c>showResultAfterSubmit</c>. Không đủ ba điều kiện thì <see cref="AttemptDetailQuestionDto.CorrectAnswerId"/>
/// và <see cref="AttemptDetailQuestionDto.IsCorrect"/> <b>vắng mặt khỏi JSON</b> (§3 quy tắc ⑥).
/// </remarks>
public sealed record AttemptDetailDto
{
    public required int AttemptId { get; init; }

    public required int ExamId { get; init; }

    public required string ExamTitle { get; init; }

    public required string Topic { get; init; }

    public required int StudentId { get; init; }

    public required string StudentName { get; init; }

    public required string StudentCode { get; init; }

    public required string Status { get; init; }

    public required DateTime StartedAt { get; init; }

    public DateTime? SubmittedAt { get; init; }

    public required bool AutoSubmitted { get; init; }

    /// <summary>Số phút em đó thực sự dùng, tính từ lúc bắt đầu tới lúc nộp.</summary>
    public int? DurationUsedMinutes { get; init; }

    public decimal? Score { get; init; }

    public decimal? MaxScore { get; init; }

    public int? CorrectCount { get; init; }

    /// <summary>
    /// Người gọi có được xem đáp án đúng hay không.
    /// </summary>
    /// <remarks>
    /// Server tính rồi trả về thay vì để giao diện tự suy từ ba điều kiện. Hai nơi cùng suy một
    /// quy tắc ba vế thì sớm muộn cũng hiểu khác nhau, và bên hiểu sai ở đây là bên để lộ đáp án.
    /// </remarks>
    public required bool CanSeeAnswers { get; init; }

    public required IReadOnlyList<AttemptDetailQuestionDto> Questions { get; init; }
}

/// <summary>Một câu trong bài đã làm, kèm lựa chọn của học sinh.</summary>
public sealed record AttemptDetailQuestionDto
{
    /// <summary>Mã dòng trong bản đề, không phải mã câu hỏi gốc trong ngân hàng.</summary>
    public required int PaperQuestionId { get; init; }

    public required int Ordinal { get; init; }

    /// <summary>Bản chụp lúc sinh đề. Giáo viên sửa câu hỏi sau đó không làm đổi bài đã chấm.</summary>
    public required string Content { get; init; }

    public string? ImageUrl { get; init; }

    public required decimal Point { get; init; }

    public required IReadOnlyList<AttemptDetailAnswerDto> Answers { get; init; }

    /// <summary>Đáp án em đã chọn. NULL nghĩa là bỏ trống câu này.</summary>
    public int? SelectedAnswerId { get; init; }

    /// <summary>Vắng mặt khỏi JSON khi người gọi chưa được phép xem đáp án.</summary>
    public int? CorrectAnswerId { get; init; }

    /// <summary>Vắng mặt cùng lúc với <see cref="CorrectAnswerId"/>.</summary>
    public bool? IsCorrect { get; init; }
}

/// <summary>Một phương án trả lời trong bản đề.</summary>
/// <remarks>
/// Cố ý <b>không có</b> trường <c>isCorrect</c>. Đáp án đúng nếu được phép xem thì đi qua
/// <see cref="AttemptDetailQuestionDto.CorrectAnswerId"/> — đúng một trường, một chỗ để kiểm.
/// Gắn cờ lên từng phương án nghĩa là mỗi lần thêm một nhánh trả dữ liệu lại phải nhớ lọc nó.
/// </remarks>
public sealed record AttemptDetailAnswerDto
{
    public required int Id { get; init; }

    public required int Ordinal { get; init; }

    public required string Content { get; init; }

    public string? ImageUrl { get; init; }
}
