namespace Anthill.Application.DTOs.Students;

/// <summary>
/// Một học sinh dưới góc nhìn quản trị viên và giáo viên. Khớp từng trường với interface
/// <c>Student</c> của frontend (<c>src/types/index.ts</c>).
/// </summary>
/// <remarks>
/// <para>
/// Ba trường cuối là <b>dữ liệu dẫn xuất</b> (§5): <c>groups</c> đọc qua JOIN, còn
/// <c>completedExams</c> và <c>averageScore</c> tính từ bảng lượt làm bài. Client gửi lên thì bỏ
/// qua — chúng không có mặt trong request nào cả, nên không tồn tại đường để gửi.
/// </para>
/// <para>
/// <b>Không</b> chứa <c>PasswordHash</c>, <c>SecurityStamp</c> hay <c>MustChangePassword</c>:
/// hai cột đầu không có lý do rời server, cột thứ ba là việc riêng giữa học sinh và màn hình
/// đăng nhập của chính họ.
/// </para>
/// </remarks>
public sealed record StudentDto
{
    public required int Id { get; init; }

    public required string FullName { get; init; }

    public required string Email { get; init; }

    public required string Username { get; init; }

    /// <summary>
    /// Số điện thoại. Chỉ có mặt khi người gọi là quản trị viên (nhà trường quản lý thông tin này); giáo viên
    /// không nhận trường này.
    /// </summary>
    public string? Phone { get; init; }

    /// <summary>Mã học sinh, duy nhất toàn hệ thống.</summary>
    public required string Code { get; init; }

    /// <summary>
    /// Các lớp em đang học (một em học được nhiều lớp cùng lúc), theo tên. Mảng rỗng là chưa có lớp nào.
    /// </summary>
    /// <remarks>
    /// Giáo viên chỉ nhận những lớp <b>mình dạy</b> (đã công bố): lớp của đồng nghiệp mà em cùng học không phải chuyện giáo
    /// viên này được biết. Quản trị viên nhận đủ.
    /// </remarks>
    public required IReadOnlyList<StudentGroupRefDto> Groups { get; init; }

    /// <summary>Chữ thường: <c>"active" | "locked" | "pendingDeletion" | "deleted"</c>, khớp union <c>StudentStatus</c> của frontend.</summary>
    public required string Status { get; init; }

    /// <summary>
    /// Vì sao tài khoản bị yêu cầu xoá. Chỉ có mặt khi đã có yêu cầu xoá.
    /// </summary>
    /// <remarks>
    /// Giữ lại cả sau khi dữ liệu cá nhân bị xoá: lúc đó họ tên đã biến mất, và đây là thứ duy nhất
    /// còn giải thích được vì sao lớp hụt một em.
    /// </remarks>
    public string? DeletionReason { get; init; }

    /// <summary>Lúc yêu cầu xoá được ghi nhận. Chỉ có mặt khi đã có yêu cầu xoá.</summary>
    public DateTime? DeletionRequestedAt { get; init; }

    /// <summary>
    /// Lúc dữ liệu cá nhân sẽ bị xoá vĩnh viễn. Chỉ có mặt khi tài khoản đang chờ xoá.
    /// </summary>
    /// <remarks>
    /// Tính sẵn ở server chứ không để giao diện tự cộng 60 ngày: con số 60 là chính sách của hệ
    /// thống, và để hai bên cùng giữ một bản thì ngày đổi chính sách là ngày hai bên nói khác nhau.
    /// </remarks>
    public DateTime? DeletionScheduledAt { get; init; }

    /// <summary>ISO 8601 UTC khi serialize, đúng quy ước §1. Do server đặt, không nhận từ client.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Số lượt ở trạng thái submitted hoặc graded (§5).</summary>
    public required int CompletedExams { get; init; }

    /// <summary>
    /// Trung bình điểm thang 10 trên mọi bài ĐÃ GIAO cho em: bài chưa làm tính 0 điểm (giống bảng điểm xuất ra Excel). Chỉ có nghĩa khi
    /// <see cref="AssignedExams"/> lớn hơn 0; bằng 0 khi chưa được giao bài nào.
    /// </summary>
    public required decimal AverageScore { get; init; }

    /// <summary>Số bài đã giao cho em. Bằng 0 thì em chưa có điểm trung bình để hiện (khác với điểm trung bình bằng 0).</summary>
    public int AssignedExams { get; init; }

    /// <summary>
    /// Em đã bắt đầu ít nhất một bài kiểm tra, ở bất kỳ trạng thái nào (đang làm dở, đã nộp, đã hết giờ).
    /// </summary>
    /// <remarks>
    /// Khác <see cref="CompletedExams"/> ở chỗ đếm cả lượt chưa nộp, nên giao diện hiện được "Đang làm bài" cho em đã vào
    /// thi mà chưa nộp. Không còn là điều kiện khoá lớp: em học được nhiều lớp cùng lúc nên chuyển hay gỡ không cần biết em
    /// đã làm bài chưa.
    /// </remarks>
    public required bool HasStartedExams { get; init; }
}
