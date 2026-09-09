using System.Globalization;
using System.Security.Claims;
using EduQuiz.Application.Interfaces.Security;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Api.Security;

/// <summary>
/// Cài đặt <see cref="ICurrentUser"/> bằng claim của <c>HttpContext.User</c>.
/// </summary>
/// <remarks>
/// Lớp này nằm ở tầng Api chứ không phải Infrastructure vì nó phụ thuộc <c>HttpContext</c> —
/// thứ chỉ tồn tại trong một request HTTP. Tầng Application chỉ thấy interface, nên service
/// viết test được bằng một bản giả lập vài dòng (§3 quy tắc ④).
/// <para>
/// Tên claim đọc ở đây khớp từng ký tự với <c>JwtTokenGenerator</c>. Điều đó chỉ đúng khi
/// <c>JwtBearerOptions.MapInboundClaims = false</c> — bật ánh xạ mặc định thì <c>sub</c> bị đổi
/// tên thành <c>ClaimTypes.NameIdentifier</c> và mọi phép đọc dưới đây trả NULL. Xem
/// <c>ServiceCollectionExtensions.AddJwtAuthentication</c>.
/// </para>
/// </remarks>
public sealed class CurrentUser : ICurrentUser
{
    public const string UserIdClaimType = "sub";
    public const string RoleClaimType = "role";
    public const string SubjectIdClaimType = "subject_id";
    public const string SecurityStampClaimType = "security_stamp";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    /// <summary>
    /// Vẫn đọc dự phòng <c>ClaimTypes.NameIdentifier</c>: nếu một ngày nào đó cấu hình bật lại
    /// ánh xạ claim mặc định thì danh tính không im lặng biến mất.
    /// </summary>
    public int? UserId => ReadInt(UserIdClaimType) ?? ReadInt(ClaimTypes.NameIdentifier);

    public UserRole? Role
    {
        get
        {
            var raw = ReadString(RoleClaimType) ?? ReadString(ClaimTypes.Role);

            return Enum.TryParse<UserRole>(raw, ignoreCase: true, out var role) && Enum.IsDefined(role)
                ? role
                : null;
        }
    }

    public int? SubjectId => ReadInt(SubjectIdClaimType);

    public string? SecurityStamp => ReadString(SecurityStampClaimType);

    /// <inheritdoc />
    public int RequireUserId() => UserId
        ?? throw new InvalidOperationException(
            "Không đọc được danh tính người gọi. Endpoint này phải nằm sau [Authorize].");

    public bool IsInRole(UserRole role) => Role == role;

    private string? ReadString(string claimType)
    {
        var value = Principal?.FindFirst(claimType)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private int? ReadInt(string claimType) =>
        int.TryParse(ReadString(claimType), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
