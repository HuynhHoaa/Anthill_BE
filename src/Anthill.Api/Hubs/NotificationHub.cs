using System.Globalization;
using Anthill.Api.Security;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Anthill.Api.Hubs;

/// <summary>
/// Hub thông báo realtime <c>/hubs/notifications</c> (§7).
/// </summary>
/// <remarks>
/// <para>
/// Hub <b>không xử lý nghiệp vụ</b>. Nó chỉ làm hai việc: xếp mỗi kết nối vào đúng group, và từ
/// chối những yêu cầu vào group không được phép. Sáu sự kiện của §7 đều do tầng Application phát ra
/// qua <c>IRealtimeNotificationService</c>, sau khi transaction nghiệp vụ đã commit.
/// </para>
/// <para>
/// Token đọc từ query string <c>access_token</c> chứ không phải header <c>Authorization</c>: trình
/// duyệt không cho đặt header khi mở WebSocket. Phần xử lý nằm ở <c>OnMessageReceived</c> trong
/// <c>ServiceCollectionExtensions.AddJwtAuthentication</c>.
/// </para>
/// </remarks>
[Authorize]
public sealed class NotificationHub : Hub
{
    public const string Route = "/hubs/notifications";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(IUnitOfWork unitOfWork, ILogger<NotificationHub> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>Group riêng của một người dùng — nơi nhận mọi thông báo cá nhân.</summary>
    public static string UserGroup(int userId) =>
        $"user:{userId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Group theo dõi một đề, để màn hình giám sát tự tăng <c>submittedCount</c> (§7).</summary>
    public static string ExamGroup(int examId) =>
        $"exam:{examId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Mỗi kết nối tự vào group riêng của chính người dùng đó. Client <b>không</b> phải gọi thêm gì,
    /// và cũng không có cách nào tự chọn group người khác.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId is null)
        {
            // Đã qua [Authorize] mà vẫn thiếu claim sub nghĩa là token sai hình dạng.
            Context.Abort();
            return;
        }

        // Không tra lại tài khoản ở đây. Token của tài khoản bị khoá, chờ xoá hay đã xoay stamp bị
        // OnTokenValidated (AddJwtAuthentication) chặn ngay ở request negotiate và request mở kết
        // nối, trước khi hub được dựng — nên một phép tra ở đây không bao giờ còn gặp token cũ, mà
        // bản trước đó lại chỉ biết mỗi trạng thái Locked.
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Giáo viên theo dõi tiến độ nộp bài của một đề.
    /// </summary>
    /// <remarks>
    /// <b>Bắt buộc kiểm tra quyền sở hữu trước khi cho vào group</b> (§7): nếu không, bất kỳ ai đăng
    /// nhập được cũng gọi <c>JoinExamGroup(id)</c> với id đoán bừa và ngồi nghe toàn bộ luồng nộp bài
    /// của lớp người khác — tên học sinh và điểm số đi thẳng qua WebSocket.
    /// </remarks>
    public async Task JoinExamGroup(int examId)
    {
        var userId = GetUserId() ?? throw new HubException("Phiên đăng nhập không hợp lệ.");
        var role = GetRole();

        if (role is not (UserRole.Teacher or UserRole.Admin))
        {
            throw new HubException("Chỉ giáo viên mới theo dõi được bài kiểm tra.");
        }

        // Quản trị viên đọc được mọi đề (§4.6) nên không cần kiểm sở hữu; giáo viên thì có.
        if (role == UserRole.Teacher
            && !await _unitOfWork.Exams.IsOwnedByAsync(examId, userId, Context.ConnectionAborted))
        {
            _logger.LogWarning(
                "Người dùng {UserId} thử theo dõi đề {ExamId} không thuộc quyền sở hữu.",
                userId,
                examId);

            // Cùng một thông điệp cho "không sở hữu" và "không tồn tại", để không dò được id nào có
            // thật — cùng lý do §4.2 chọn 404 thay vì 403 ở REST.
            throw new HubException("Không tìm thấy bài kiểm tra này.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ExamGroup(examId), Context.ConnectionAborted);
    }

    /// <summary>
    /// Rời group theo dõi. Không cần kiểm quyền: rời một group mình không ở trong là thao tác vô hại.
    /// </summary>
    public Task LeaveExamGroup(int examId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ExamGroup(examId), Context.ConnectionAborted);

    /// <summary>
    /// Đồng hồ server, để client hiệu chỉnh phần đếm ngược mà không phải gọi REST.
    /// Hạn giờ thật vẫn tính bằng <c>ExpiresAt</c> trong database (§3 quy tắc ③).
    /// </summary>
    public DateTime GetServerTime() => DateTime.UtcNow;

    /// <summary>
    /// SignalR không tự dọn group khi kết nối đóng — nhưng nó cũng không cần: group chỉ là danh sách
    /// connectionId, và connectionId chết thì bị loại khỏi mọi group.
    /// </summary>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogDebug(exception, "Kết nối hub {ConnectionId} đóng do lỗi.", Context.ConnectionId);
        }

        return base.OnDisconnectedAsync(exception);
    }

    private int? GetUserId() =>
        int.TryParse(
            Context.User?.FindFirst(CurrentUser.UserIdClaimType)?.Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var userId)
            ? userId
            : null;

    private UserRole? GetRole() =>
        Enum.TryParse<UserRole>(
            Context.User?.FindFirst(CurrentUser.RoleClaimType)?.Value,
            ignoreCase: true,
            out var role) && Enum.IsDefined(role)
            ? role
            : null;
}
