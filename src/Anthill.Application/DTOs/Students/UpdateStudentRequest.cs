namespace Anthill.Application.DTOs.Students;

/// <summary>
/// Thân request của <c>PUT /api/students/{id}</c> — chỉ quản trị viên gọi được (§4.3).
/// </summary>
/// <remarks>
/// <b>Không có trường mật khẩu, và sẽ không bao giờ có</b> (§4.3). Đổi mật khẩu người khác là
/// một hành động khác hẳn việc sửa hồ sơ: nó phải xoay <c>SecurityStamp</c> để đá mọi phiên đang
/// mở, phải trả lại chuỗi mới cho quản trị viên, và phải bật <c>MustChangePassword</c>. Gộp vào
/// đây thì một lần lưu hồ sơ lỡ tay cũng đủ khoá học sinh ra ngoài giữa giờ làm bài. Việc đó
/// thuộc về <c>POST /api/students/{id}/reset-password</c>.
/// </remarks>
public sealed record UpdateStudentRequest
{
    public required string FullName { get; init; }

    public required string Email { get; init; }

    public required string Username { get; init; }

    public required string Code { get; init; }

    /// <summary>
    /// Số điện thoại. <c>null</c> (không gửi) giữ nguyên số đang có; chuỗi rỗng xoá trắng. Học sinh không tự
    /// sửa được số này, chỉ quản trị viên.
    /// </summary>
    public string? Phone { get; init; }

    /// <summary>Bỏ trống hoặc 0 nghĩa là gỡ khỏi nhóm hiện tại, không xoá tài khoản.</summary>
    public int? GroupId { get; init; }

    /// <summary><c>"active"</c> hoặc <c>"locked"</c>. Bỏ trống thì giữ nguyên trạng thái đang có.</summary>
    public string? Status { get; init; }
}
