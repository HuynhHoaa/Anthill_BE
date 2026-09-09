namespace EduQuiz.Application.DTOs.ExamAssignments;

/// <summary>
/// Kết quả một lần giao bài: đã giao được bao nhiêu em, và những em không giao được thì vì sao.
/// </summary>
/// <remarks>
/// <para>
/// Frontend hiện chỉ đọc <see cref="AssignedCount"/>. Bốn con số còn lại vẫn được trả về vì im
/// lặng ở đây là im lặng đúng chỗ nguy hiểm nhất: giáo viên chọn cả lớp 40 em, hệ thống báo "đã
/// giao", nhưng 3 em có tài khoản đang khoá thì không hề có bài. Đến hôm thi mới phát hiện thì đã
/// muộn. Cùng khuôn với <c>AddMembersResponse</c> của bước 2.
/// </para>
/// <para>
/// Giao lại cho người đã có bài <b>không</b> phải là lỗi — endpoint cố ý idempotent (§5) — nên nó
/// được đếm riêng chứ không nằm chung với các mục bị bỏ qua vì trục trặc.
/// </para>
/// </remarks>
public sealed record AssignExamResponse
{
    /// <summary>Số assignment <b>mới</b> được tạo trong lần gọi này.</summary>
    public required int AssignedCount { get; init; }

    /// <summary>Đã có bài từ trước nên bỏ qua, giữ nguyên trạng thái và điểm đang có.</summary>
    public required int SkippedAlreadyAssigned { get; init; }

    /// <summary>Tài khoản đang bị khoá — bỏ qua, vì các em không đăng nhập được để làm bài.</summary>
    public required int SkippedLocked { get; init; }

    /// <summary>Id không tồn tại, hoặc tồn tại nhưng không phải tài khoản học sinh.</summary>
    public required int SkippedNotFound { get; init; }

    /// <summary>
    /// Học sinh không thuộc nhóm nào do chính giáo viên này quản lý.
    /// </summary>
    /// <remarks>
    /// Bỏ qua trong im lặng thay vì trả 403 cho cả request: giáo viên chọn nhầm một em trong danh
    /// sách vài chục người thì phần còn lại vẫn phải giao được. Con số này cho họ biết có chuyện
    /// gì đã xảy ra.
    /// </remarks>
    public required int SkippedOutOfScope { get; init; }

    /// <summary>
    /// Số <b>nhóm lớp</b> bị bỏ qua vì không do giáo viên này quản lý.
    /// </summary>
    /// <remarks>
    /// Đếm riêng khỏi <see cref="SkippedOutOfScope"/> vì hai con số khác đơn vị. Gộp chung thì một
    /// lớp 40 em bị từ chối lại hiện ra là "bỏ qua 1", và giáo viên đọc con số đó sẽ tưởng chỉ có
    /// một em gặp vấn đề.
    /// </remarks>
    public required int SkippedOutOfScopeGroups { get; init; }
}
