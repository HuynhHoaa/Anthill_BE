namespace EduQuiz.Application.DTOs.StudentGroups;

/// <summary>
/// Thân request của <c>POST</c> và <c>PUT /api/student-groups</c>.
/// </summary>
/// <remarks>
/// <para>
/// Một DTO dùng cho cả tạo lẫn sửa vì cả hai nhận đúng hai trường như nhau — tách đôi chỉ để
/// "cho đối xứng" thì mỗi lần thêm trường phải sửa hai chỗ, và một ngày nào đó chúng lệch nhau.
/// </para>
/// <para>
/// Cố ý <b>không</b> có <c>OwnerTeacherId</c>: chủ sở hữu lấy từ JWT (§4.4). Nhận từ body nghĩa
/// là giáo viên A tạo được nhóm rồi gán cho giáo viên B, hoặc cướp nhóm của B bằng một lần PUT.
/// Cũng không có <c>StudentCount</c> — sĩ số là dữ liệu dẫn xuất, client gửi lên thì bỏ qua (§5).
/// </para>
/// </remarks>
public sealed record SaveStudentGroupRequest
{
    public required string Name { get; init; }

    /// <summary>Tuỳ chọn. Chuỗi rỗng nghĩa là xoá trắng mô tả.</summary>
    public string? Description { get; init; }
}
