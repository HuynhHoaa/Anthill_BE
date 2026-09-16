using Anthill.Application.Interfaces.Security;

namespace Anthill.Infrastructure.Authentication;

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

    /// <summary>
    /// Chuỗi băm giả, sinh một lần cho cả tiến trình bằng đúng <see cref="WorkFactor"/> ở trên.
    /// </summary>
    /// <remarks>
    /// Sinh chứ không viết cứng: chuỗi viết cứng đóng băng work factor của ngày nó được dán vào, nên
    /// nâng work factor thật lên là lặng lẽ mở lại đúng kênh dò thời gian mà nó sinh ra để bịt. Nội
    /// dung băm là một chuỗi ngẫu nhiên không ai biết, và không tài khoản nào mang chuỗi băm này.
    /// <para>
    /// <see cref="Lazy{T}"/> tĩnh: băm ở work factor 12 tốn khoảng 250ms, trả giá đó đúng một lần ở
    /// lần đăng nhập sai tên đầu tiên chứ không phải mỗi lần khởi tạo lớp.
    /// </para>
    /// </remarks>
    private static readonly Lazy<string> DecoyHash = new(
        () => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), WorkFactor));

    /// <inheritdoc />
    public string TimingDecoyHash => DecoyHash.Value;

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
