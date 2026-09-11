namespace Anthill.Domain.Entities;

/// <summary>
/// Phân công một giáo viên phụ trách một lớp.
/// </summary>
/// <remarks>
/// <para>
/// Quan hệ nhiều–nhiều: một lớp có nhiều giáo viên (mỗi môn một người), và một giáo viên dạy nhiều
/// lớp. Đó là lý do phải có bảng riêng thay vì một cột <c>TeacherId</c> trên lớp.
/// </para>
/// <para>
/// Cố ý <b>không</b> kế thừa <c>BaseEntity</c>: bảng này không có khoá chính riêng mà dùng khoá kép
/// (lớp, giáo viên). Khoá kép chính là thứ khiến "phân công một người hai lần vào cùng một lớp"
/// trở thành điều database từ chối, thay vì một quy tắc tầng ứng dụng phải nhớ kiểm.
/// </para>
/// </remarks>
public class StudentGroupTeacher
{
    public int StudentGroupId { get; set; }

    public StudentGroup StudentGroup { get; set; } = null!;

    public int TeacherId { get; set; }

    public User Teacher { get; set; } = null!;

    public DateTime AssignedAt { get; set; }
}
