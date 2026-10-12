using System.Text;
using Anthill.Application.Validators;

namespace Anthill.Infrastructure.Authentication;

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

    public string Issuer { get; set; } = "Anthill";

    public string Audience { get; set; } = "Anthill.Client";

    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Phần bù cộng thêm vào bài kiểm tra dài nhất được phép: học sinh đăng nhập trước giờ làm bài một chút, và
    /// cửa sổ ân hạn nộp trễ cũng cần token còn sống.
    /// </summary>
    public const int ExamMarginMinutes = 30;

    /// <summary>
    /// Mốc thấp nhất của <see cref="ExpiryMinutes"/>: bài dài nhất mà validator cho phép cộng phần bù. Thấp hơn thì
    /// một học sinh làm bài đủ dài sẽ hết phiên giữa buổi thi, và vì chưa có refresh token nên không có đường cấp lại.
    /// </summary>
    public static int MinimumExpiryMinutes => SaveExamRequestValidator.MaxDurationMinutes + ExamMarginMinutes;

    /// <summary>
    /// Mặc định 480 phút (8 giờ). Phải dài hơn bài kiểm tra dài nhất được phép; token chết sớm hơn thì vẫn có
    /// <c>SecurityStamp</c> thu hồi ngay khi khoá hay đổi mật khẩu, nên sống lâu không làm mất khả năng cắt phiên.
    /// </summary>
    public int ExpiryMinutes { get; set; } = 480;

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

        if (ExpiryMinutes < MinimumExpiryMinutes)
        {
            throw new InvalidOperationException(
                $"'{SectionName}:ExpiryMinutes' ({ExpiryMinutes}) phải từ {MinimumExpiryMinutes} phút trở lên: bài kiểm tra dài nhất "
                + $"được phép là {SaveExamRequestValidator.MaxDurationMinutes} phút, token ngắn hơn thì học sinh hết phiên giữa buổi thi.");
        }
    }
}
