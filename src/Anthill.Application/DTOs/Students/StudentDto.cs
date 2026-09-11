namespace Anthill.Application.DTOs.Students;

/// <summary>
/// Một học sinh dưới góc nhìn quản trị viên và giáo viên. Khớp từng trường với interface
/// <c>Student</c> của frontend (<c>src/types/index.ts</c>).
/// </summary>
/// <remarks>
/// <para>
/// Ba trường cuối là <b>dữ liệu dẫn xuất</b> (§5): <c>group</c> đọc qua JOIN, còn
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

    /// <summary>Mã học sinh, duy nhất toàn hệ thống.</summary>
    public required string Code { get; init; }

    /// <summary>
    /// Tên nhóm lớp, JOIN từ <c>StudentGroups</c>. Học sinh chưa thuộc nhóm nào nhận nhãn
    /// "Chưa phân nhóm" thay vì chuỗi rỗng — frontend khai báo trường này không nullable.
    /// </summary>
    public required string Group { get; init; }

    /// <summary>0 nghĩa là chưa thuộc nhóm nào, xem <see cref="Common.StudentGroupDefaults"/>.</summary>
    public required int GroupId { get; init; }

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

    /// <summary>Trung bình điểm trên các lượt đã chấm, làm tròn 1 chữ số (§5). Bằng 0 khi chưa làm bài nào.</summary>
    public required decimal AverageScore { get; init; }
}
