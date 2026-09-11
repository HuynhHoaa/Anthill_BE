namespace EduQuiz.Application.DTOs.ExamAssignments;

/// <summary>
/// Một học sinh đã được giao bài, kèm tiến trình làm bài của em đó — màn hình theo dõi của giáo viên.
/// </summary>
public sealed record ExamAssignmentDto
{
    public required int Id { get; init; }

    public required int ExamId { get; init; }

    public required int StudentId { get; init; }

    public required string StudentName { get; init; }

    /// <summary>Mã học sinh. Chuỗi rỗng nếu tài khoản chưa được cấp mã.</summary>
    public required string StudentCode { get; init; }

    /// <summary>Tên nhóm lớp, hoặc chuỗi rỗng khi em chưa được phân nhóm.</summary>
    public required string Group { get; init; }

    /// <summary>
    /// <c>"assigned"</c> · <c>"inprogress"</c> · <c>"submitted"</c> · <c>"graded"</c> · <c>"expired"</c>.
    /// </summary>
    /// <remarks>
    /// Dữ liệu <b>dẫn xuất</b>, không có cột nào lưu nó. Chưa có lượt làm bài thì là <c>assigned</c>;
    /// có rồi thì lấy trạng thái của lượt đó.
    ///
    /// Một trường hợp được suy thêm: khung giờ của em đã đóng (lịch riêng của lớp nếu có, ngược lại
    /// lịch chung của đề) mà em chưa từng bắt đầu thì trả <c>expired</c> chứ không phải <c>assigned</c>. Để nguyên <c>assigned</c> sau khi hết hạn khiến bảng theo dõi
    /// đọc như thể em vẫn còn cơ hội làm bài, trong khi cửa đã đóng — và giáo viên không phân biệt
    /// được "chưa làm nhưng còn kịp" với "hết giờ mà không vào thi".
    /// </remarks>
    public required string Status { get; init; }

    /// <summary>NULL khi em chưa bắt đầu làm bài.</summary>
    public int? AttemptId { get; init; }

    /// <summary>Chỉ có nghĩa khi bài đã chấm. NULL ở mọi trạng thái khác.</summary>
    public decimal? Score { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? SubmittedAt { get; init; }

    public required DateTime AssignedAt { get; init; }

    /// <summary>Giờ mở bài THẬT của em này: khung giờ riêng của lớp nếu có, ngược lại là lịch chung của đề.</summary>
    /// <remarks>
    /// Trả giờ đã phân giải chứ không trả cột thô: cột NULL nghĩa là "theo đề", và bắt giao diện tự
    /// ghép lại là mời nó ghép sai. Có trường này giáo viên mới hiểu vì sao một em đã
    /// <c>expired</c> trong khi thẻ đề vẫn báo đang mở, và thấy được lần gửi lại khung giờ đã vào hay chưa.
    /// </remarks>
    public required DateTime OpenAt { get; init; }

    /// <summary>Giờ đóng bài THẬT của em này. Cùng quy tắc với <see cref="OpenAt"/>.</summary>
    public required DateTime CloseAt { get; init; }

    /// <summary>
    /// true khi bản ghi đang theo khung giờ riêng; false khi theo lịch chung của đề — sửa lịch đề sẽ kéo theo.
    /// </summary>
    public required bool HasScheduleOverride { get; init; }

    /// <summary>Lớp mà bài được giao qua đó. NULL (vắng khỏi JSON) khi giáo viên chọn đích danh em này.</summary>
    public int? AssignedViaGroupId { get; init; }
}
