using Anthill.Domain.Common;
using Anthill.Domain.Enums;

namespace Anthill.Domain.Entities;

/// <summary>
/// Một dòng trong lịch sử của lớp: lớp ra đời với ai, giáo viên phụ trách đổi từ ai sang ai, lúc nào, vì sao.
/// </summary>
/// <remarks>
/// <para>
/// <b>Chỉ thêm, không sửa, không xoá</b>, cùng lý do với <see cref="AccountChange"/>: lý do là thứ quản trị viên gõ để
/// người đọc sau này hiểu vì sao lớp đổi người dạy, sửa lại được thì nó không còn là bằng chứng.
/// </para>
/// <para>
/// Tên giáo viên được chép lại thay vì chỉ giữ khoá ngoại: giáo viên có thể đổi tên, còn dòng lịch sử phải đọc ra đúng
/// như lúc nó xảy ra. Vì vậy hai cột mã giáo viên cố ý không có khoá ngoại. Lớp bị xoá thì lịch sử của nó đi theo.
/// </para>
/// </remarks>
public class GroupChange : BaseEntity
{
    public int GroupId { get; set; }

    public StudentGroup Group { get; set; } = null!;

    public GroupChangeKind Kind { get; set; }

    /// <summary>Lý do do quản trị viên nhập. NULL khi lớp ra đời hoặc khi lớp chưa có giáo viên nào để "đổi".</summary>
    public string? Reason { get; set; }

    public int? FromTeacherId { get; set; }

    public string? FromTeacherName { get; set; }

    public int? ToTeacherId { get; set; }

    public string? ToTeacherName { get; set; }

    /// <summary>Quản trị viên thực hiện. Có thể NULL nếu tài khoản đó bị gỡ khỏi hệ thống sau này.</summary>
    public int? ActorUserId { get; set; }

    public User? Actor { get; set; }
}
