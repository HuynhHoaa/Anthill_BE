namespace EduQuiz.Application.DTOs.Questions;

/// <summary>
/// Thống kê ngân hàng câu hỏi — <c>GET /api/questions/summary</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Gộp ba lời gọi mà frontend đang bắn song song</b> (§4.5): <c>listTopics()</c>,
/// <c>countByDifficulty()</c>, và một lần <c>listQuestions()</c> chỉ để lấy con số tổng. Ba request
/// cho một màn hình là ba lần chịu độ trễ mạng, và trên dịch vụ miễn phí có cold start thì cả ba
/// cùng đánh thức tiến trình.
/// </para>
/// <para>
/// Ba con số theo mức độ tính trên <b>toàn bộ</b> ngân hàng trong phạm vi người gọi, kể cả câu
/// đang ẩn — giống hệt <c>countByDifficulty()</c> của frontend. Số câu <i>dùng được để sinh đề</i>
/// là chuyện khác và thuộc về bước xem trước blueprint, nơi chỉ đếm câu <c>Active</c>.
/// </para>
/// </remarks>
public sealed record QuestionBankSummaryDto
{
    public required int Total { get; init; }

    public required int Active { get; init; }

    public required int Hidden { get; init; }

    public required int Easy { get; init; }

    public required int Medium { get; init; }

    public required int Hard { get; init; }

    /// <summary>Tập chủ đề đang có, đã sắp xếp — đủ để đổ vào dropdown mà không cần gọi thêm.</summary>
    public required IReadOnlyList<string> Topics { get; init; }
}
