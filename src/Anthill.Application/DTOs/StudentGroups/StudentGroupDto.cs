namespace Anthill.Application.DTOs.StudentGroups;

/// <summary>Một lớp học của trung tâm.</summary>
public sealed record StudentGroupDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>
    /// Sĩ số: học sinh đang hoạt động hoặc đang khoá. Không tính tài khoản chờ xoá, dù những tài
    /// khoản đó vẫn có mặt trong danh sách thành viên cho tới khi được khôi phục hoặc xoá hẳn.
    /// </summary>
    public required int StudentCount { get; init; }

    /// <summary>
    /// Lớp đã công bố hay còn là bản nháp.
    /// </summary>
    /// <remarks>
    /// Giáo viên và học sinh không bao giờ nhận về lớp chưa công bố — server lọc sẵn ở tầng truy
    /// vấn. Trường này có mặt để màn hình quản trị phân biệt được hai loại, không phải để giao
    /// diện tự lọc.
    /// </remarks>
    public required bool IsPublished { get; init; }

    /// <summary>NULL khi lớp còn là bản nháp.</summary>
    public DateTime? PublishedAt { get; init; }

    /// <summary>
    /// Giáo viên được phân công phụ trách lớp.
    /// </summary>
    /// <remarks>
    /// Mảng rỗng là tín hiệu đáng chú ý với quản trị viên: một lớp chưa có ai dạy thì bài kiểm tra
    /// không bao giờ tới được học sinh trong đó. Đây chính là câu hỏi mà mô hình cũ — giáo viên tự
    /// tạo lớp của mình — không có cách nào trả lời.
    /// </remarks>
    public required IReadOnlyList<GroupTeacherDto> Teachers { get; init; }

    public required DateTime CreatedAt { get; init; }
}

/// <summary>Một giáo viên được phân công vào lớp.</summary>
public sealed record GroupTeacherDto
{
    public required int Id { get; init; }

    public required string FullName { get; init; }

    public required string Email { get; init; }

    /// <summary>Môn giáo viên phụ trách. Chuỗi rỗng khi tài khoản chưa được gán môn.</summary>
    public required string Subject { get; init; }

    public required string AvatarColor { get; init; }

    /// <summary>
    /// <c>active</c> hoặc <c>locked</c>.
    /// </summary>
    /// <remarks>
    /// Có mặt vì danh sách giáo viên khả dụng cố ý bao gồm cả tài khoản đang bị khoá. Thiếu trường
    /// này thì giao diện không phân biệt được, người dùng chọn xong bấm gửi rồi mới biết mình chọn
    /// hụt qua con số <c>skippedLocked</c> — một cái ngõ cụt hoàn toàn tránh được.
    /// </remarks>
    public required string Status { get; init; }
}
