using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>
/// Xoá sạch dữ liệu cá nhân của một tài khoản, giữ lại dòng dữ liệu.
/// </summary>
/// <remarks>
/// <para>
/// Đây là chỗ dung hoà hai đòi hỏi trái nhau: người rời hệ thống thì dữ liệu cá nhân phải biến mất
/// thật, còn bảng điểm giáo viên đã dựng thì không được thủng một dòng. Xoá hẳn bản ghi không làm
/// được — khoá ngoại từ bảng giao bài sang tài khoản để <c>Restrict</c> đúng vì lý do đó.
/// </para>
/// <para>
/// <b>Điểm khó:</b> <c>Email</c>, <c>Username</c> và <c>Code</c> đều có chỉ mục duy nhất. Không
/// thể để trống hàng loạt vì dòng thứ hai sẽ đụng dòng thứ nhất, nên chúng được thay bằng giá trị
/// sinh từ id — vừa duy nhất, vừa không mang thông tin gì về người cũ.
/// </para>
/// </remarks>
public static class StudentAnonymizer
{
    /// <summary>Tên hiển thị thay cho tài khoản đã xoá, dùng chung ở mọi bảng kết quả.</summary>
    public const string DisplayName = "Học sinh đã xoá";

    /// <summary>
    /// Số ngày chờ trước khi dữ liệu cá nhân bị xoá.
    /// </summary>
    /// <remarks>
    /// Sáu mươi ngày là khoảng đủ để một quyết định sai lộ ra: "em đó chuyển trường" hoá ra nhầm
    /// với em khác cùng họ, và người phát hiện thường là giáo viên chủ nhiệm ở kỳ họp tháng sau.
    /// </remarks>
    public const int RetentionDays = 60;

    /// <summary>Xoá dữ liệu cá nhân tại chỗ. Không đụng tới lượt làm bài, điểm, hay lịch sử.</summary>
    public static void Anonymize(User user, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Hậu tố sinh từ id: duy nhất theo đúng nghĩa của chỉ mục, và không suy ngược ra được gì
        // về người cũ. Dùng id chứ không dùng chuỗi ngẫu nhiên để chạy lại hai lần cho cùng kết quả.
        var token = $"deleted-{user.Id}";

        user.FullName = DisplayName;
        user.Email = $"{token}@deleted.local";
        user.Username = token;
        user.Code = token;
        user.Phone = null;
        user.School = null;

        // Đổi mật khẩu thành một chuỗi không ai biết, phòng trường hợp có đường đăng nhập nào đó
        // lọt qua kiểm tra trạng thái. Xoay SecurityStamp để mọi token đã phát chết ngay.
        user.PasswordHash = Guid.NewGuid().ToString("N");
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        // Rời khỏi lớp: sĩ số lớp phải phản ánh số em đang thật sự học ở đó.
        user.GroupId = null;

        user.Status = UserStatus.Deleted;
        user.DeletedAt = nowUtc;
        user.UpdatedAt = nowUtc;

        // DeletionReason và DeletionRequestedAt CỐ Ý giữ nguyên: chúng là thứ duy nhất còn giải
        // thích được vì sao dòng này tồn tại mà không có tên.
    }

    /// <summary>Đã tới hạn xoá chưa.</summary>
    public static bool IsDue(User user, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.Status == UserStatus.PendingDeletion
               && user.DeletionRequestedAt is { } requestedAt
               && nowUtc >= requestedAt.AddDays(RetentionDays);
    }
}
