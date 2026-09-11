using Anthill.Application.Common;

namespace Anthill.Application.Interfaces.Security;

/// <summary>
/// Đọc đúng phần tài khoản mà <see cref="SessionValidation.Evaluate"/> cần, cho mỗi request có token.
/// </summary>
/// <remarks>
/// Tách khỏi <c>IUserRepository</c> vì hai thứ có nhu cầu trái nhau: repository trả entity để service
/// sửa rồi lưu, còn đường này chạy trên <b>mọi</b> request nên phải là một truy vấn theo khoá chính,
/// không theo dõi thay đổi, chỉ ba cột.
/// </remarks>
public interface IAccountSessionReader
{
    /// <returns>NULL khi không còn tài khoản nào mang id này.</returns>
    Task<AccountSessionState?> GetAsync(int userId, CancellationToken cancellationToken = default);
}
