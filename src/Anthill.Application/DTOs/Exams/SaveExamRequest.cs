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

    /// <summary>
    /// Số câu và tỷ lệ ba mức độ của đề <b>theo ma trận</b>. Đề gửi kèm <see cref="Questions"/> thì bỏ qua
    /// cả nhóm này: server tính lại từ danh sách câu hỏi.
    /// </summary>
    public int TotalQuestions { get; init; }

    public int EasyPercentage { get; init; }

    public int MediumPercentage { get; init; }

    public int HardPercentage { get; init; }

    /// <summary>
    /// Điểm của một câu ở từng mức độ, cho đề theo ma trận. Tổng (số câu mỗi mức × điểm mức đó) chuẩn là
    /// 10 nhưng server không bắt buộc: giao diện báo "n / 10" và hỏi xác nhận trước khi chốt một đề chưa đủ.
    /// Đề theo danh sách câu hỏi đặt điểm từng câu ở <see cref="Questions"/>.
    /// </summary>
    public decimal PointEasy { get; init; }

    public decimal PointMedium { get; init; }

    public decimal PointHard { get; init; }

    /// <summary>
    /// Danh sách câu hỏi giáo viên đã chọn, theo thứ tự, kèm điểm từng câu. Có mặt (kể cả rỗng) nghĩa là đề
    /// soạn theo danh sách và phần ma trận ở trên bị bỏ qua; vắng mặt là đề theo ma trận như trước.
    /// </summary>
    /// <remarks>
    /// Đề đã soạn theo danh sách thì mọi lần PUT đều phải gửi lại trường này: danh sách là một phần của
    /// "thay toàn bộ cấu hình", và gửi thiếu nó sẽ khiến đề bị hiểu nhầm là đang quay về ma trận.
    /// </remarks>
    public IReadOnlyList<ExamQuestionInput>? Questions { get; init; }

    /// <summary>
    /// Số đề nhỏ mà danh sách câu hỏi (đề lớn) được chia ra cho mỗi lớp, từ 1 đến <c>PaperPlanner.MaxPaperCount</c>.
    /// 1 (mặc định) là một bản đề chung như trước. Từ 2 trở lên chỉ dùng được với đề theo danh sách (<see cref="Questions"/>
    /// có mặt), và khi đó nhóm trường ma trận <see cref="TotalQuestions"/>, <see cref="EasyPercentage"/>,
    /// <see cref="MediumPercentage"/>, <see cref="HardPercentage"/> là cấu hình của <b>một đề nhỏ</b> (số câu mỗi đề và tỷ lệ
    /// Dễ/Trung bình/Khó), không bị bỏ qua. Điểm vẫn đặt ở từng câu trong <see cref="Questions"/>, nhưng các câu cùng mức độ
    /// phải cùng điểm để mọi đề có cùng tổng điểm.
    /// </summary>
    public int PaperCount { get; init; } = 1;

    /// <summary>
    /// Số đơn vị 0,01 điểm cộng thêm cho các câu mức cao nhất của <b>mỗi</b> đề nhỏ, để đề nhỏ cộng đúng 10 khi các câu cùng mức
    /// cùng điểm (hai chữ số thập phân) không chia ra đúng 10 — ví dụ 3 / 3 / 3 câu. 0 (mặc định) là không cộng; chỉ có nghĩa khi
    /// <see cref="PaperCount"/> từ 2 trở lên, và không lớn hơn số câu của một đề.
    /// </summary>
    public int PaperBonusUnits { get; init; }

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

/// <summary>Một dòng của danh sách câu hỏi trong <see cref="SaveExamRequest.Questions"/>.</summary>
public sealed record ExamQuestionInput
{
    public required int QuestionId { get; init; }

    /// <summary>Điểm của riêng câu này trong đề, tối đa hai chữ số thập phân.</summary>
    public required decimal Point { get; init; }
}
