namespace Anthill.Api.Configuration;

/// <summary>Cấu hình cho <c>DemoAccountGuard</c> — xem <c>IDemoAccountGuard</c> (§H19).</summary>
public sealed class DemoAccountProtectionOptions
{
    public const string SectionName = "DemoAccountProtection";

    /// <summary>
    /// Định danh (email, username hoặc mã học sinh) của các tài khoản demo cần bảo vệ. So khớp
    /// không phân biệt hoa thường sau khi trim, đối chiếu với cả ba cột Email/Username/Code của
    /// từng tài khoản — ba cột đó dùng chung một không gian tên (§H4), nên định danh nào cũng nhận
    /// diện đúng tài khoản dù ghi ở cột nào.
    /// </summary>
    /// <remarks>
    /// Giá trị mặc định khớp ba tài khoản <c>DbSeeder</c> tạo ra — chính là ba tài khoản in công
    /// khai trên trang đăng nhập. Đây không phải id viết chết trong code: đổi được bằng cấu hình
    /// (biến môi trường <c>DemoAccountProtection__ProtectedIdentifiers__0</c>...) nếu một môi
    /// trường khác seed dữ liệu demo với định danh khác.
    /// </remarks>
    public IReadOnlyList<string> ProtectedIdentifiers { get; set; } =
    [
        "huy.pq@anthill.edu.vn",
        "an.nv@anthill.edu.vn",
        "lan.tt@student.edu.vn"
    ];
}
