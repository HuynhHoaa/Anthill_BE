using System.Globalization;
using Anthill.Application.Interfaces.Security;
using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>
/// Ba cột của tài khoản mà phép kiểm phiên cần — không hơn. Đọc cả entity cho mỗi request có token
/// là kéo theo họ tên, email, chuỗi băm mật khẩu chỉ để so hai giá trị.
/// </summary>
public sealed record AccountSessionState(int Id, UserStatus Status, string SecurityStamp);

/// <summary>Kết luận về một JWT đã qua kiểm chữ ký và hạn dùng.</summary>
public enum SessionVerdict
{
    /// <summary>Tài khoản còn hoạt động và stamp trong token khớp database.</summary>
    Valid,

    /// <summary>Thiếu hoặc hỏng claim <c>sub</c> / <c>security_stamp</c>. Token do hệ thống cấp luôn có đủ hai claim.</summary>
    MalformedToken,

    /// <summary>Không còn dòng tài khoản nào mang id này.</summary>
    AccountMissing,

    Locked,

    PendingDeletion,

    Deleted,

    /// <summary>Tài khoản vẫn hoạt động nhưng stamp đã xoay: đổi mật khẩu, được cấp lại mật khẩu, hoặc đã từng bị khoá.</summary>
    StampChanged
}

/// <summary>
/// Một chỗ duy nhất trả lời "token này còn đại diện cho một phiên hợp lệ không".
/// </summary>
/// <remarks>
/// <para>
/// Chữ ký hợp lệ chỉ chứng minh token do chính server cấp, không chứng minh tài khoản vẫn còn như lúc
/// cấp. Khoá, đưa vào diện xoá, đổi hay cấp lại mật khẩu đều xoay <c>SecurityStamp</c>; hàm này đối
/// chiếu stamp trong token với database để những thay đổi đó có hiệu lực ngay, không phải chờ token
/// hết hạn.
/// </para>
/// <para>
/// <see cref="Evaluate"/> là hàm thuần để kiểm được mọi nhánh bằng unit test; phần tra database nằm sau
/// <c>IAccountSessionReader</c>, và <see cref="CheckAsync"/> ghép hai thứ lại. Tầng Api gọi nó ở hai
/// cửa: <c>OnTokenValidated</c> cho mọi request HTTP, và bộ lọc hub cho mỗi lần gọi phương thức trên
/// một kết nối SignalR đã mở.
/// </para>
/// </remarks>
public static class SessionValidation
{
    private const string AccountLockedMessage = "Tài khoản đã bị khoá, liên hệ quản trị viên.";

    /// <summary>Đọc hai claim của token, tra tài khoản, rồi kết luận.</summary>
    /// <param name="subjectClaim">Giá trị claim <c>sub</c>.</param>
    /// <param name="stampClaim">Giá trị claim <c>security_stamp</c>.</param>
    /// <remarks>
    /// Một hàm cho cả hai cửa để REST và hub không thể hiểu khác nhau về cùng một token. Claim hỏng hay
    /// thiếu thì kết luận ngay, không tốn truy vấn nào: token như vậy chưa bao giờ do hệ thống cấp.
    /// </remarks>
    public static async Task<SessionVerdict> CheckAsync(
        string? subjectClaim,
        string? stampClaim,
        IAccountSessionReader reader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (!TryReadUserId(subjectClaim, out var userId) || string.IsNullOrWhiteSpace(stampClaim))
        {
            return SessionVerdict.MalformedToken;
        }

        var account = await reader.GetAsync(userId, cancellationToken);
        return Evaluate(stampClaim, account);
    }

    /// <summary>Đọc claim <c>sub</c>. Id không dương thì không trỏ tới dòng nào, coi như token hỏng.</summary>
    public static bool TryReadUserId(string? subjectClaim, out int userId) =>
        int.TryParse(subjectClaim, NumberStyles.None, CultureInfo.InvariantCulture, out userId) && userId > 0;

    /// <param name="stampClaim">Giá trị claim <c>security_stamp</c> trong token.</param>
    /// <param name="account">Tài khoản đọc từ database; NULL khi không còn dòng nào.</param>
    /// <remarks>
    /// <para>
    /// Thiếu stamp là <b>từ chối</b>, không phải "không có gì để so": mọi token hệ thống cấp đều mang
    /// claim này, nên một token không có nó chỉ có thể là token cũ từ trước khi có stamp hoặc token
    /// giả — cả hai đều không nên lọt qua.
    /// </para>
    /// <para>
    /// Trạng thái xét <b>trước</b> stamp dù khoá và đưa vào diện xoá đều xoay stamp: kết luận
    /// <see cref="SessionVerdict.Locked"/> cho ra câu "liên hệ quản trị viên", còn
    /// <see cref="SessionVerdict.StampChanged"/> chỉ nói "đăng nhập lại" — mà đăng nhập lại thì cũng
    /// bị chặn.
    /// </para>
    /// </remarks>
    public static SessionVerdict Evaluate(string? stampClaim, AccountSessionState? account)
    {
        if (string.IsNullOrWhiteSpace(stampClaim))
        {
            return SessionVerdict.MalformedToken;
        }

        if (account is null)
        {
            return SessionVerdict.AccountMissing;
        }

        switch (account.Status)
        {
            case UserStatus.Active:
                break;
            case UserStatus.PendingDeletion:
                return SessionVerdict.PendingDeletion;
            case UserStatus.Deleted:
                return SessionVerdict.Deleted;
            default:
                // Locked, và mọi trạng thái thêm vào sau này mà chưa ai nghĩ tới: đóng cửa trước.
                return SessionVerdict.Locked;
        }

        // Ordinal: stamp là chuỗi máy sinh, không có khái niệm hoa thường hay văn hoá.
        return string.Equals(stampClaim, account.SecurityStamp, StringComparison.Ordinal)
            ? SessionVerdict.Valid
            : SessionVerdict.StampChanged;
    }

    /// <summary>Ba kết luận do trạng thái tài khoản gây ra, chứ không phải do token.</summary>
    public static bool IsBlockedAccount(SessionVerdict verdict) =>
        verdict is SessionVerdict.Locked or SessionVerdict.PendingDeletion or SessionVerdict.Deleted;

    /// <summary>Câu chữ trả về cho người cầm token bị từ chối.</summary>
    /// <remarks>
    /// Nói cụ thể tài khoản bị khoá hay chờ xoá không làm lộ gì: người gọi đang cầm một token có chữ
    /// ký thật của chính tài khoản đó.
    /// </remarks>
    public static string MessageFor(SessionVerdict verdict) => verdict switch
    {
        SessionVerdict.Locked => BlockedStatusMessage(UserStatus.Locked),
        SessionVerdict.PendingDeletion => BlockedStatusMessage(UserStatus.PendingDeletion),
        SessionVerdict.Deleted => BlockedStatusMessage(UserStatus.Deleted),
        SessionVerdict.StampChanged =>
            "Phiên đăng nhập đã kết thúc vì tài khoản vừa thay đổi (đổi mật khẩu, cấp lại mật khẩu hoặc bị khoá). Vui lòng đăng nhập lại.",
        _ => "Phiên đăng nhập không còn hợp lệ. Vui lòng đăng nhập lại."
    };

    /// <summary>
    /// Câu chữ cho từng trạng thái không cho vào hệ thống. Đăng nhập, <c>/api/auth/me</c> và phép kiểm
    /// phiên dùng chung, để cùng một trạng thái không mang hai cách nói.
    /// </summary>
    public static string BlockedStatusMessage(UserStatus status) => status switch
    {
        // Cùng một người để liên hệ với câu của tài khoản bị khoá: chính quản trị viên là người đưa
        // tài khoản vào diện xoá, và là người duy nhất khôi phục được.
        UserStatus.PendingDeletion =>
            "Tài khoản đang chờ xoá, liên hệ quản trị viên nếu đây là nhầm lẫn.",
        UserStatus.Deleted =>
            "Tài khoản này không còn tồn tại.",
        _ => AccountLockedMessage
    };
}
