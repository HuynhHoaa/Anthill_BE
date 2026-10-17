namespace Anthill.Application.DTOs.Auth;

/// <summary>
/// Thân request của <c>PUT /api/auth/me</c> — sửa hồ sơ của <b>chính mình</b>.
/// </summary>
/// <remarks>
/// Ai sửa được gì: quản trị viên sửa cả ba trường; giáo viên chỉ đổi được <c>FullName</c> (email và số điện
/// thoại do quản trị viên quản lý, gửi giá trị khác đi bị 400); học sinh không đổi được trường nào. Form vẫn
/// gửi đủ ba trường, nên gửi lại đúng giá trị đang có là hợp lệ.
/// 
/// Cố ý không có <c>Id</c>, <c>Role</c>, <c>Status</c>, <c>Code</c> hay <c>GroupId</c>: danh tính
/// suy từ JWT, còn vai trò và nhóm lớp là việc của quản trị viên/giáo viên chứ không phải của
/// người dùng tự sửa hồ sơ. Frontend có gửi kèm <c>subject</c> ở màn hình giáo viên; trường đó
/// không khai báo ở đây nên bị bỏ qua khi deserialize — môn của giáo viên gắn bằng
/// <c>SubjectId</c> do quản trị viên đặt, không đổi bằng cách gõ tên môn.
/// </remarks>
public sealed record UpdateProfileRequest
{
    public required string FullName { get; init; }

    public required string Email { get; init; }

    /// <summary>Chuỗi rỗng nghĩa là xoá trắng số điện thoại (chỉ quản trị viên làm được).</summary>
    public string? Phone { get; init; }
}
