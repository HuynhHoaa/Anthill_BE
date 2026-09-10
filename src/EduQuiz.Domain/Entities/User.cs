using EduQuiz.Domain.Common;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Domain.Entities;

/// <summary>
/// Một bảng dùng chung cho cả ba vai trò. Các cột chỉ có nghĩa với một vai trò
/// (<see cref="SubjectId"/>, <see cref="Code"/>, <see cref="GroupId"/>) đều nullable.
/// </summary>
public class User : BaseEntity
{
    public required string FullName { get; set; }

    public required string Email { get; set; }

    public required string Username { get; set; }

    public required string PasswordHash { get; set; }

    /// <summary>
    /// Đổi giá trị này là vô hiệu hoá tức thì mọi JWT còn hạn của tài khoản —
    /// dùng khi khoá tài khoản, đổi mật khẩu hoặc admin reset mật khẩu.
    /// </summary>
    public required string SecurityStamp { get; set; }

    public UserRole Role { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;

    /// <summary>Chỉ giáo viên mới có môn. NULL với Admin và Student.</summary>
    public int? SubjectId { get; set; }

    /// <summary>Mã học sinh, duy nhất. NULL với Admin và Teacher.</summary>
    public string? Code { get; set; }

    /// <summary>Nhóm lớp của học sinh. Gỡ khỏi nhóm là đặt về NULL, không xoá tài khoản.</summary>
    public int? GroupId { get; set; }

    public string? Phone { get; set; }

    public string? School { get; set; }

    public required string AvatarColor { get; set; }

    /// <summary>Bật khi admin cấp/reset mật khẩu; ép đổi ở lần đăng nhập kế tiếp.</summary>
    public bool MustChangePassword { get; set; }

    public Subject? Subject { get; set; }

    public StudentGroup? Group { get; set; }

    /// <summary>Lớp do quản trị viên này lập ra. Chỉ để ghi nhận, không mang ý nghĩa phân quyền.</summary>
    public ICollection<StudentGroup> CreatedGroups { get; set; } = new List<StudentGroup>();

    /// <summary>
    /// Lớp mà giáo viên này được phân công phụ trách.
    /// </summary>
    /// <remarks>
    /// Đây mới là thứ quyết định giáo viên thấy lớp nào và giao bài được cho lớp nào — trước đây
    /// vai trò đó thuộc về <c>OwnedGroups</c>, tức là lớp do chính họ tạo ra.
    /// </remarks>
    public ICollection<StudentGroupTeacher> TeachingGroups { get; set; } = new List<StudentGroupTeacher>();

    public ICollection<Question> CreatedQuestions { get; set; } = new List<Question>();

    public ICollection<Exam> CreatedExams { get; set; } = new List<Exam>();

    /// <summary>Bài được giao cho học sinh này.</summary>
    public ICollection<ExamAssignment> Assignments { get; set; } = new List<ExamAssignment>();

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
