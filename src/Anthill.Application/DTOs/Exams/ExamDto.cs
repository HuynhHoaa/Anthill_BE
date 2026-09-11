namespace Anthill.Application.DTOs.Exams;

/// <summary>
/// Một đề kiểm tra dưới góc nhìn giáo viên — §4.6.
/// </summary>
/// <remarks>
/// <para>
/// Đề lưu dưới dạng <b>cấu hình</b>, không chứa danh sách câu hỏi: câu hỏi chỉ xuất hiện khi sinh
/// <c>ExamPaper</c> lúc giao bài. Nhờ vậy sửa tỷ lệ độ khó của một đề chưa giao không phải dọn dẹp
/// gì cả.
/// </para>
/// <para>
/// Trường <c>status</c> mà kiểu <c>Exam</c> ở frontend đang khai báo <b>không</b> có mặt ở đây.
/// Trong mock nó luôn mang giá trị <c>'assigned'</c> và không màn hình nào đọc tới; còn tiến trình
/// làm bài là thuộc tính của <i>từng assignment</i>, không phải của đề. Đưa lên một trường luôn
/// bằng một hằng số chỉ để khớp kiểu là mời người đọc sau này tin vào nó.
/// </para>
/// </remarks>
public sealed record ExamDto
{
    public required int Id { get; init; }

    public required string Title { get; init; }

    /// <summary>Chuỗi rỗng chứ không phải NULL — frontend hiển thị thẳng, không kiểm NULL.</summary>
    public required string Description { get; init; }

    /// <summary>Chủ đề dùng để lọc khi sinh đề. Chuỗi rỗng nghĩa là lấy từ mọi chủ đề.</summary>
    public required string Topic { get; init; }

    public required int DurationMinutes { get; init; }

    public required int TotalQuestions { get; init; }

    public required int EasyPercentage { get; init; }

    public required int MediumPercentage { get; init; }

    public required int HardPercentage { get; init; }

    public required decimal PointPerQuestion { get; init; }

    public required bool ShuffleQuestions { get; init; }

    public required bool ShuffleAnswers { get; init; }

    /// <summary>
    /// Bật: mỗi học sinh một bản đề riêng. Giá trị này được chốt ở bước <b>giao bài</b> (§4.6),
    /// nên ở đây nó chỉ để đọc — biết đề đã giao theo kiểu nào.
    /// </summary>
    public required bool RandomizePerStudent { get; init; }

    public required bool ShowResultAfterSubmit { get; init; }

    public required DateTime OpenAt { get; init; }

    public required DateTime CloseAt { get; init; }

    /// <summary>
    /// <c>"draft"</c> · <c>"scheduled"</c> · <c>"open"</c> · <c>"closed"</c>.
    /// Dữ liệu <b>dẫn xuất</b>: chỉ <c>IsDraft</c> nằm trong database, ba giá trị còn lại tính từ
    /// <see cref="OpenAt"/>/<see cref="CloseAt"/> so với giờ server ngay lúc đọc (§5). Vì vậy một
    /// đề có thể tự chuyển từ <c>scheduled</c> sang <c>open</c> mà không ai gọi endpoint nào.
    /// </summary>
    public required string Lifecycle { get; init; }

    /// <summary>Số assignment của đề. Backend đếm, không nhận từ client (§5).</summary>
    public required int AssignedCount { get; init; }

    /// <summary>Số assignment đã ở trạng thái <c>submitted</c> hoặc <c>graded</c>.</summary>
    public required int SubmittedCount { get; init; }

    /// <summary>
    /// Số assignment đã có lượt làm bài — đang làm, đã nộp hay đã hết giờ đều tính. Luôn
    /// <c>≥ SubmittedCount</c>.
    /// </summary>
    public required int StartedCount { get; init; }

    /// <summary>
    /// <c>true</c> khi đã có em bắt đầu làm bài (<see cref="StartedCount"/> &gt; 0). Từ lúc đó số câu,
    /// ba tỷ lệ, chủ đề, điểm mỗi câu, thời lượng và hai tuỳ chọn trộn không đổi được nữa — muốn khác
    /// thì nhân bản đề. Server tính sẵn để giao diện không phải tự suy lại luật khoá.
    /// </summary>
    public required bool BlueprintFrozen { get; init; }

    public required DateTime CreatedAt { get; init; }
}
