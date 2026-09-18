using Anthill.Api.Configuration;
using Anthill.Application.Interfaces.Security;
using Anthill.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Anthill.Api.Security;

/// <summary>
/// Cài đặt <see cref="IDemoAccountGuard"/> bằng danh sách định danh đọc từ
/// <see cref="DemoAccountProtectionOptions"/>.
/// </summary>
/// <remarks>
/// Nằm ở tầng Api cùng lý do với <c>CurrentUser</c>: đây là chỗ duy nhất <see cref="IOptions{TOptions}"/>
/// của Api được phép rò vào — tầng Application chỉ thấy interface.
/// </remarks>
public sealed class DemoAccountGuard : IDemoAccountGuard
{
    private readonly HashSet<string> _identifiers;

    public DemoAccountGuard(IOptions<DemoAccountProtectionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var identifier in options.Value.ProtectedIdentifiers)
        {
            if (Normalize(identifier) is { } key)
            {
                _identifiers.Add(key);
            }
        }
    }

    /// <inheritdoc />
    public bool IsProtected(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (_identifiers.Count == 0)
        {
            return false;
        }

        // Đối chiếu cả ba cột: §H4 coi Email/Username/Code là một không gian tên chung, nên một
        // định danh cấu hình có thể trùng bất kỳ cột nào tuỳ vai trò (email cho giáo viên/quản trị,
        // username hoặc mã học sinh cho học sinh).
        return (Normalize(user.Email) is { } email && _identifiers.Contains(email))
               || (Normalize(user.Username) is { } username && _identifiers.Contains(username))
               || (Normalize(user.Code) is { } code && _identifiers.Contains(code));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
