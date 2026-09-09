namespace EduQuiz.Application.DTOs.Exams;

/// <summary>
/// Cấu hình blueprint đang gõ dở, gửi lên <c>POST /api/exams/blueprint-preview</c>.
/// </summary>
/// <remarks>
/// Client chỉ gửi <b>tỷ lệ</b>; số câu từng mức do backend tính (§5). Nhận số câu do client tính
/// sẵn nghĩa là hai nơi cùng giữ một công thức, và tới lúc chúng lệch nhau thì không ai biết bên
/// nào đúng.
/// </remarks>
public sealed record BlueprintInput
{
    public required int TotalQuestions { get; init; }

    public required int EasyPercentage { get; init; }

    public required int MediumPercentage { get; init; }

    public required int HardPercentage { get; init; }

    /// <summary>Tên chủ đề, hoặc <c>"all"</c>/bỏ trống để lấy từ mọi chủ đề.</summary>
    public string? Topic { get; init; }
}
