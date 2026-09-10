namespace EduQuiz.Domain.Enums;

public enum UserStatus
{
    Active,

    /// <summary>Tạm thời không đăng nhập được, nhưng tài khoản vẫn nguyên vẹn.</summary>
    Locked,

    /// <summary>
    /// Đã yêu cầu xoá, đang trong thời gian chờ.
    /// </summary>
    /// <remarks>
    /// Không đăng nhập được và bị ẩn khỏi mọi danh sách vận hành, nhưng dữ liệu còn nguyên nên
    /// khôi phục được. Hết thời gian chờ, tiến trình nền xoá sạch dữ liệu cá nhân và chuyển sang
    /// <see cref="Deleted"/>.
    ///
    /// Tồn tại vì xoá người là thao tác không hoàn tác được, mà lý do xoá thường chỉ sai lộ ra sau
    /// vài ngày — "em đó chuyển trường" hoá ra là nhầm với em khác cùng họ.
    /// </remarks>
    PendingDeletion,

    /// <summary>
    /// Dữ liệu cá nhân đã bị xoá; dòng dữ liệu giữ lại để kết quả các bài đã làm không mất chủ.
    /// </summary>
    /// <remarks>
    /// Đây là chỗ dung hoà hai điều mâu thuẫn nhau: người rời hệ thống thì dữ liệu cá nhân của họ
    /// phải biến mất thật, còn bảng điểm mà giáo viên đã dựng thì không được thủng một dòng.
    ///
    /// Xoá hẳn dòng dữ liệu là không làm được — khoá ngoại từ bảng giao bài sang tài khoản để
    /// <c>Restrict</c> đúng vì lý do đó. Nên tên, email, số điện thoại bị xoá, còn lượt làm bài và
    /// điểm ở lại, hiển thị dưới tên "Học sinh đã xoá".
    /// </remarks>
    Deleted
}
