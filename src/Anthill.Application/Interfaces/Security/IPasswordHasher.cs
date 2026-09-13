namespace Anthill.Application.Interfaces.Security;

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

    /// <summary>
    /// Một chuỗi băm hợp lệ không thuộc tài khoản nào, để đốt đúng lượng thời gian mà một lần đối
    /// chiếu mật khẩu thật tiêu tốn.
    /// </summary>
    /// <remarks>
    /// Chuỗi này <b>phải</b> do chính bản cài đặt sinh ra, bằng đúng thuật toán và work factor của
    /// <see cref="Hash"/>. Một chuỗi viết cứng sẵn thường mang work factor khác — và mỗi bậc work
    /// factor là gấp đôi thời gian, nên nhánh "không có tài khoản này" trả lời nhanh gấp đôi nhánh
    /// "sai mật khẩu", đủ để dò ra tài khoản nào có thật chỉ bằng đồng hồ bấm giây.
    /// </remarks>
    string TimingDecoyHash { get; }
}
