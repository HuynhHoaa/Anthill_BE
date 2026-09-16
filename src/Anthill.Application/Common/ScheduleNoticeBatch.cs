using Anthill.Application.Interfaces.Realtime;

namespace Anthill.Application.Common;

/// <summary>Những gì cần bắn realtime sau commit cho một lần đổi giờ.</summary>
/// <remarks>
/// Payload được dựng ngay lúc thêm thông báo, trên chính đề và bản ghi của lần đổi đó, để bước bắn sau commit
/// không phải đọc lại database — một lượt đọc hỏng ở đó biến lần đổi giờ đã ghi xong thành lỗi trả về.
/// </remarks>
public sealed class ScheduleNoticeBatch
{
    private readonly List<(int StudentId, ExamUpdatedEvent Payload)> _events = [];
    private readonly Dictionary<int, int> _unreadCounts = [];

    public static ScheduleNoticeBatch Empty => new();

    public IReadOnlyList<(int StudentId, ExamUpdatedEvent Payload)> Events => _events;

    public IReadOnlyDictionary<int, int> UnreadCounts => _unreadCounts;

    public bool IsEmpty => _events.Count == 0;

    public void Add(int studentId, ExamUpdatedEvent payload) => _events.Add((studentId, payload));

    public void SetUnreadCount(int studentId, int unreadCount) => _unreadCounts[studentId] = unreadCount;
}
