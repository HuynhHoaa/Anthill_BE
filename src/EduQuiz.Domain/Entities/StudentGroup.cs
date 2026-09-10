using EduQuiz.Domain.Common;

namespace EduQuiz.Domain.Entities;

/// <summary>
/// Một lớp học của trung tâm hoặc nhà trường.
/// </summary>
/// <remarks>
/// <para>
/// Lớp là <b>đơn vị hành chính</b>, do quản trị viên lập ra — không phải nhóm học do từng giáo viên
/// tự gom. Điều đó đến từ chính lược đồ: <c>User.GroupId</c> là một khoá ngoại đơn, nên một học
/// sinh chỉ thuộc đúng một lớp. Nếu để mỗi giáo viên sở hữu lớp của riêng mình thì hai giáo viên
/// cùng dạy một em sẽ tranh nhau em đó — người thêm sau kéo em ra khỏi lớp của người trước, và
/// mọi bài người trước đã giao mất chỗ dựa.
/// </para>
/// <para>
/// Giáo viên được <b>phân công</b> vào lớp qua <see cref="Teachers"/> chứ không tự chọn. Nhờ vậy
/// quản trị viên nhìn ra ngay lớp nào chưa có ai dạy — điều mà mô hình "giáo viên tự chọn lớp"
/// không bao giờ trả lời được.
/// </para>
/// </remarks>
public class StudentGroup : BaseEntity
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Quản trị viên đã lập lớp này. Chỉ để ghi nhận, không mang ý nghĩa phân quyền.</summary>
    /// <remarks>
    /// Khác hẳn <c>OwnerTeacherId</c> trước đây: trường cũ là thứ chặn giáo viên A xem lớp của
    /// giáo viên B. Giờ việc chặn đó do <see cref="Teachers"/> đảm nhiệm, còn trường này chỉ trả
    /// lời câu "ai đã tạo".
    /// </remarks>
    public int CreatedByUserId { get; set; }

    public User CreatedBy { get; set; } = null!;

    /// <summary>
    /// Lớp đã công bố hay còn là bản nháp.
    /// </summary>
    /// <remarks>
    /// Quản trị viên cần dựng xong một lớp — đặt tên, xếp học sinh, phân công giáo viên — rồi mới
    /// cho nó xuất hiện. Không có trạng thái này thì một lớp mới tạo, chưa có ai trong đó, đã hiện
    /// ngay trên màn hình của giáo viên; và một lớp rỗng thì giao bài cho nó cũng chẳng tới ai.
    ///
    /// Cùng khuôn với bản nháp của đề kiểm tra, để người dùng chỉ phải học một khái niệm.
    /// </remarks>
    public bool IsPublished { get; set; }

    /// <summary>NULL khi lớp còn là bản nháp.</summary>
    public DateTime? PublishedAt { get; set; }

    public ICollection<User> Students { get; set; } = new List<User>();

    /// <summary>Giáo viên được phân công phụ trách lớp này.</summary>
    public ICollection<StudentGroupTeacher> Teachers { get; set; } = new List<StudentGroupTeacher>();
}
