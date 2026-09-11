namespace Anthill.Application.DTOs.Notifications;

/// <summary>Một thông báo gửi tới đúng một người dùng.</summary>
/// <remarks>
/// Cố ý <b>không có</b> trường <c>audience</c> như bản mock. Mock lọc thông báo theo vai trò, nghĩa
/// là mọi giáo viên nhìn thấy chung một dòng thông báo; ở đây mỗi bản ghi thuộc về đúng một
/// <c>RecipientUserId</c>, nên "thông báo của tôi" là một phép lọc theo khoá chính chứ không phải
/// theo nhóm người.
/// </remarks>
public sealed record NotificationDto
{
    public required int Id { get; init; }

    /// <summary>
    /// <c>examAssigned</c> · <c>examSubmitted</c> · <c>examUpdated</c> · <c>deadlineSoon</c> ·
    /// <c>autoSubmitted</c>.
    /// </summary>
    public required string Type { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    /// <summary>Đường dẫn trong ứng dụng để bấm vào. NULL khi thông báo không dẫn tới đâu.</summary>
    public string? Link { get; init; }

    public required bool IsRead { get; init; }

    public required DateTime CreatedAt { get; init; }
}
