namespace Anthill.Application.DTOs.Exams;

/// <summary>
/// Thân request của <c>POST /api/exams</c> và <c>PUT /api/exams/{id}</c>.
/// </summary>
/// <remarks>
/// <para>
/// Một kiểu dùng chung cho cả tạo và sửa, vì §4.6 quy định PUT là <b>thay toàn bộ cấu hình</b>
/// chứ không vá từng trường. Muốn đổi riêng lịch mở/đóng thì đã có
/// <c>PATCH /api/exams/{id}/schedule</c> — hẹp hơn nên an toàn hơn.
/// </para>
/// <para>
/// Ở đây <b>không</b> có <c>assignedCount</c>, <c>submittedCount</c> hay <c>lifecycle</c> dạng
/// tính sẵn: cả ba là dữ liệu dẫn xuất, backend tự tính (§5). Nhận chúng từ client là mở đường
/// cho một request sửa tay biến đề đã đóng thành đang mở.
/// </para>
/// </remarks>
public sealed record SaveExamRequest
{
    public required string Title { get; init; }

    /// <summary>Bỏ trống được. NULL và chuỗi rỗng được đối xử như nhau.</summary>
    public string? Description { get; init; }

    /// <summary>Chủ đề để lọc câu hỏi khi sinh đề. Bỏ trống hoặc <c>"all"</c> nghĩa là mọi chủ đề.</summary>
    public string? Topic { get; init; }

    public required int DurationMinutes { get; init; }

    public required int TotalQuestions { get; init; }

    public required int EasyPercentage { get; init; }

    public required int MediumPercentage { get; init; }

    public required int HardPercentage { get; init; }

    /// <summary>
    /// Điểm của một câu ở từng mức độ. Tổng (số câu mỗi mức × điểm mức đó) chuẩn là 100 nhưng server
    /// không bắt buộc: giao diện báo "n / 100" và hỏi xác nhận trước khi chốt một đề chưa đủ.
    /// </summary>
    public required decimal PointEasy { get; init; }

    public required decimal PointMedium { get; init; }

    public required decimal PointHard { get; init; }

    public bool ShuffleQuestions { get; init; }

    public bool ShuffleAnswers { get; init; }

    public bool ShowResultAfterSubmit { get; init; }

    public required DateTime OpenAt { get; init; }

    public required DateTime CloseAt { get; init; }

    /// <summary>
    /// Chỉ nhận <c>"draft"</c> — hoặc bỏ trống.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Frontend gửi <c>lifecycle: 'draft'</c> để lưu bản nháp, và bỏ trống khi muốn đề có hiệu lực
    /// thật. Ba giá trị còn lại (<c>scheduled</c>/<c>open</c>/<c>closed</c>) <b>bị từ chối</b>: chúng
    /// là dữ liệu dẫn xuất từ mốc thời gian, gửi lên đây thì hoặc bị bỏ qua trong im lặng, hoặc cho
    /// phép ép một đề sang trạng thái mâu thuẫn với chính lịch của nó.
    /// </para>
    /// <para>
    /// Bản nháp được miễn hai điều kiện nặng nhất — tổng ba tỷ lệ bằng 100 và ngân hàng đủ câu —
    /// vì mục đích của nháp là lưu lại một cấu hình <i>chưa</i> hoàn chỉnh.
    /// </para>
    /// </remarks>
    public string? Lifecycle { get; init; }
}
