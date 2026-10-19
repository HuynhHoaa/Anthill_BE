namespace Anthill.Domain.Entities;

/// <summary>
/// Một học sinh là thành viên của một lớp.
/// </summary>
/// <remarks>
/// <para>
/// Quan hệ nhiều–nhiều: <b>một học sinh học được nhiều lớp cùng lúc</b> (học cả lớp Toán lẫn lớp Anh ở trung tâm), và một lớp
/// có nhiều học sinh. Trước đây mỗi em chỉ có một cột <c>GroupId</c> và vì thế bị gò vào đúng một lớp; chuyển sang bảng này thì
/// việc "thêm em vào lớp" không còn kéo em ra khỏi lớp khác.
/// </para>
/// <para>
/// Cố ý <b>không</b> kế thừa <c>BaseEntity</c> (cùng khuôn với <see cref="StudentGroupTeacher"/>): khoá chính là cặp (lớp, học
/// sinh), nên "xếp một em hai lần vào cùng một lớp" là điều database từ chối chứ không phải quy tắc tầng ứng dụng phải nhớ.
/// </para>
/// </remarks>
public class StudentGroupMember
{
    public int StudentGroupId { get; set; }

    public StudentGroup StudentGroup { get; set; } = null!;

    public int StudentId { get; set; }

    public User Student { get; set; } = null!;

    /// <summary>Lúc em vào lớp. Với dữ liệu cũ là mốc tạo tài khoản (không còn cách nào biết chính xác hơn).</summary>
    public DateTime JoinedAt { get; set; }
}
