namespace Anthill.Application.DTOs.Exams;

/// <summary>
/// Cách một đề được chia thành các đề nhỏ — <c>GET /api/exams/{id}/papers</c>, chỉ giáo viên và quản trị viên đọc.
/// </summary>
/// <remarks>
/// Học sinh không bao giờ nhận dữ liệu này, và cũng không nhận mã đề của mình: mã đề và danh sách câu của từng đề nhỏ
/// là thứ để giáo viên đối chiếu, không phải để học sinh so với nhau.
/// </remarks>
public sealed record ExamPapersDto
{
    /// <summary>Số đề nhỏ mỗi lớp nhận (1 = đề không chia).</summary>
    public required int PaperCount { get; init; }

    /// <summary>Tính trước theo danh sách câu hiện có: mỗi mức cần bao nhiêu câu mỗi đề, đề lớn có bao nhiêu, trùng và dư bao nhiêu.</summary>
    public required IReadOnlyList<ExamPaperLevelPlanDto> Levels { get; init; }

    /// <summary>Từng lớp đã được giao: các đề nhỏ của lớp đó và số em cầm từng đề. Rỗng khi chưa giao lớp nào.</summary>
    public required IReadOnlyList<ExamPaperClassDto> Classes { get; init; }
}

public sealed record ExamPaperLevelPlanDto
{
    /// <summary><c>"easy"</c> · <c>"medium"</c> · <c>"hard"</c>.</summary>
    public required string Level { get; init; }

    /// <summary>Số câu mức này trong đề lớn.</summary>
    public required int PoolSize { get; init; }

    /// <summary>Số câu mức này mỗi đề nhỏ cần.</summary>
    public required int PerPaper { get; init; }

    /// <summary>Đề lớn có đủ câu để một đề nhỏ có các câu khác nhau.</summary>
    public required bool Sufficient { get; init; }

    /// <summary>Số câu nằm ở từ hai đề trở lên.</summary>
    public required int SharedQuestions { get; init; }

    /// <summary>Số câu không nằm ở đề nhỏ nào.</summary>
    public required int UnusedQuestions { get; init; }
}

public sealed record ExamPaperClassDto
{
    /// <summary>NULL chỉ với dữ liệu giao lẻ cũ không gắn lớp.</summary>
    public int? GroupId { get; init; }

    public required string GroupName { get; init; }

    /// <summary>Tổng số em của lớp đã có đề.</summary>
    public required int Students { get; init; }

    public required IReadOnlyList<ExamPaperVariantDto> Papers { get; init; }
}

public sealed record ExamPaperVariantDto
{
    /// <summary>Số thứ tự đề từ 0.</summary>
    public required int VariantIndex { get; init; }

    /// <summary>Mã đề: <c>"A"</c>, <c>"B"</c>…</summary>
    public required string Label { get; init; }

    /// <summary>Số em của lớp đang cầm đề này.</summary>
    public required int Students { get; init; }

    public required int QuestionCount { get; init; }

    public required int Easy { get; init; }

    public required int Medium { get; init; }

    public required int Hard { get; init; }

    /// <summary>Mã các câu hỏi của đề nhỏ này (không theo thứ tự hiển thị — mỗi em một thứ tự).</summary>
    public required IReadOnlyList<int> QuestionIds { get; init; }
}
