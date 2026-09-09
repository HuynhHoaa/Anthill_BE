using EduQuiz.Application.Interfaces.Security;

namespace EduQuiz.Infrastructure.Authentication;

/// <summary>
/// Băm mật khẩu bằng BCrypt.
/// </summary>
/// <remarks>
/// Work factor 12 — §5 đòi tối thiểu 11, lấy dư một bậc vì mỗi bậc là gấp đôi công sức của
/// người dò mật khẩu mà chỉ tốn thêm khoảng 100ms cho mỗi lần đăng nhập thật.
/// Salt nằm sẵn trong chuỗi băm nên không cần cột salt riêng.
/// </remarks>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Chuỗi băm hỏng hoặc thuộc định dạng khác — coi như sai mật khẩu, không làm sập
            // cả endpoint đăng nhập vì một bản ghi lỗi.
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
