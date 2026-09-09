using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Interfaces.Security;

/// <summary>
/// Danh tính của người gọi, đọc từ claim trong JWT.
/// </summary>
/// <remarks>
/// Đây là mấu chốt của §3 quy tắc ④: <c>listMyExams</c>, <c>startAttempt</c>, <c>saveAnswers</c>,
/// <c>submitAttempt</c>... đều <b>không</b> nhận <c>studentId</c> từ client mà lấy ở đây.
/// Có abstraction này thì service không cần biết <c>HttpContext</c>, và test viết được
/// bằng một bản giả lập vài dòng.
/// </remarks>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Claim <c>sub</c>. NULL khi chưa đăng nhập.</summary>
    int? UserId { get; }

    UserRole? Role { get; }

    /// <summary>Claim <c>subject_id</c>. Chỉ giáo viên mới có — quyết định thấy được ngân hàng nào (§6).</summary>
    int? SubjectId { get; }

    /// <summary>
    /// Claim <c>security_stamp</c>. Lệch với giá trị trong database nghĩa là tài khoản đã bị khoá
    /// hoặc đổi mật khẩu sau khi token được cấp — token còn hạn vẫn phải bị từ chối (§5).
    /// </summary>
    string? SecurityStamp { get; }

    /// <summary>Dùng ở nhánh code đã nằm sau <c>[Authorize]</c>, nơi thiếu danh tính là lỗi lập trình.</summary>
    int RequireUserId();

    bool IsInRole(UserRole role);
}
