using System.Text;

namespace EduQuiz.Infrastructure.Authentication;

/// <summary>
/// Cấu hình cấp và kiểm token. Đọc từ mục <c>Jwt</c> của <c>appsettings.json</c>, cho phép biến
/// môi trường <c>Jwt__SecretKey</c> ghi đè khi deploy.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// HS256 ký bằng khoá đối xứng; khoá ngắn hơn 256 bit thì bộ kiểm token của ASP.NET Core
    /// từ chối thẳng. 32 byte là mức tối thiểu, không phải mức khuyến nghị.
    /// </summary>
    public const int MinimumSecretKeyBytes = 32;

    public string Issuer { get; set; } = "EduQuiz";

    public string Audience { get; set; } = "EduQuiz.Client";

    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Mặc định 120 phút. Học sinh làm bài 45–60 phút nên token hết hạn giữa chừng là mất bài;
    /// đường cấp lại chính thức vẫn là refresh token (§4.1), giá trị này chỉ là lớp đệm.
    /// </summary>
    public int ExpiryMinutes { get; set; } = 120;

    /// <summary>Ném lỗi ngay khi cấu hình thiếu, thay vì cấp ra token không ai kiểm được.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            throw new InvalidOperationException(
                $"Thiếu cấu hình '{SectionName}:SecretKey'. Đặt trong appsettings hoặc biến môi trường {SectionName}__SecretKey.");
        }

        if (Encoding.UTF8.GetByteCount(SecretKey) < MinimumSecretKeyBytes)
        {
            throw new InvalidOperationException(
                $"'{SectionName}:SecretKey' phải dài tối thiểu {MinimumSecretKeyBytes} byte để ký được HS256.");
        }

        if (ExpiryMinutes <= 0)
        {
            throw new InvalidOperationException($"'{SectionName}:ExpiryMinutes' phải lớn hơn 0.");
        }
    }
}
