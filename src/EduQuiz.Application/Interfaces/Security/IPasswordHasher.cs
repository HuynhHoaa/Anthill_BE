namespace EduQuiz.Application.Interfaces.Security;

/// <summary>
/// Băm và đối chiếu mật khẩu. Cài đặt bằng BCrypt với work factor ≥ 11 (§5).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Trả về chuỗi băm đã kèm salt — không cần cột salt riêng.</summary>
    string Hash(string password);

    /// <summary>
    /// Đối chiếu mật khẩu thô với chuỗi băm đọc từ database.
    /// Thứ tự tham số: <b>mật khẩu trước, chuỗi băm sau</b>.
    /// </summary>
    bool Verify(string password, string passwordHash);
}
