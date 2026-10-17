using Anthill.Domain.Common;
using Anthill.Domain.Enums;

namespace Anthill.Domain.Entities;

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

    public required string AvatarColor { get; set; }

    /// <summary>
    /// Ảnh đại diện do giáo viên hay quản trị viên tự tải lên: đường dẫn tương đối do <c>/api/uploads/images</c> cấp.
    /// NULL là chưa có ảnh — giao diện khi đó vẽ chữ cái đầu trên nền <see cref="AvatarColor"/>.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Bật khi admin cấp/reset mật khẩu; ép đổi ở lần đăng nhập kế tiếp.</summary>
    public bool MustChangePassword { get; set; }

    public Subject? Subject { get; set; }

    /// <summary>
    /// Vì sao tài khoản này được yêu cầu xoá. NULL khi chưa ai yêu cầu.
    /// </summary>
    /// <remarks>
    /// Bắt buộc nhập khi yêu cầu xoá, và giữ lại cả sau khi dữ liệu cá nhân đã bị xoá sạch. Sáu
    /// tháng sau có người hỏi "sao lớp 12A1 hụt một em" thì đây là chỗ duy nhất còn câu trả lời —
    /// bản thân cái tên thì đã biến mất theo đúng yêu cầu.
    /// </remarks>
    public string? DeletionReason { get; set; }

    /// <summary>
    /// Trạng thái ngay trước khi vào diện xoá, để khôi phục trả tài khoản về đúng chỗ cũ.
    /// NULL khi tài khoản không nằm trong diện xoá.
    /// </summary>
    /// <remarks>
    /// Không có cột này thì khôi phục luôn ra <see cref="UserStatus.Active"/>, và một em đang bị khoá
    /// vì lý do kỷ luật được mở lại chỉ vì từng bị đưa vào danh sách chờ xoá rồi gỡ ra.
    /// </remarks>
    public UserStatus? StatusBeforeDeletion { get; set; }

    /// <summary>Mốc bắt đầu đếm thời gian chờ. NULL khi tài khoản không nằm trong diện xoá.</summary>
    public DateTime? DeletionRequestedAt { get; set; }

    /// <summary>Ai đã yêu cầu xoá.</summary>
    public int? DeletionRequestedByUserId { get; set; }

    /// <summary>Mốc dữ liệu cá nhân thật sự bị xoá. NULL nghĩa là chưa xoá.</summary>
    public DateTime? DeletedAt { get; set; }

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
