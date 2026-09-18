namespace Anthill.Api.RateLimiting;

/// <summary>Cấu hình giới hạn tần suất cho <c>POST /api/auth/login</c> và <c>/api/auth/change-password</c> (§H20).</summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "AuthRateLimit";

    /// <summary>Số request tối đa cho mỗi cặp (địa chỉ IP, định danh) trong một cửa sổ thời gian.</summary>
    /// <remarks>
    /// Đủ rộng để không chặn nhầm lưu lượng hợp lệ: bộ kiểm HTTP của chính repo này đăng nhập lặp đi
    /// lặp lại trên cùng một tài khoản trong vài chục giây (xem
    /// <c>tests/api-checks/session-revocation.sh</c> — kịch bản chạy đua khoá/cấp lại mật khẩu đăng
    /// nhập lại vài chục lần chỉ để dựng dữ liệu kiểm chứng, không phải hành vi dò mật khẩu). Vẫn đủ
    /// chặt để một cuộc dò mật khẩu tự động không thử được quá vài chục lần một phút trên cùng một
    /// tài khoản từ cùng một địa chỉ.
    /// </remarks>
    public int PermitLimit { get; set; } = 60;

    /// <summary>Độ dài cửa sổ, tính bằng giây.</summary>
    public int WindowSeconds { get; set; } = 60;
}
