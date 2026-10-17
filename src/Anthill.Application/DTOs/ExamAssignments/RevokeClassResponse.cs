namespace Anthill.Application.DTOs.ExamAssignments;

/// <summary>Kết quả thu hồi bài đã giao cho một lớp.</summary>
/// <remarks>
/// Thu hồi cả lớp chỉ gỡ được những em <b>chưa bắt đầu</b> làm: gỡ một em đã vào thi là xoá luôn phần bài em
/// đã làm. Em đã bắt đầu vẫn giữ bài, nên hai con số được trả riêng để giáo viên biết lớp vẫn còn bao nhiêu em
/// đang theo bài này.
/// </remarks>
public sealed record RevokeClassResponse
{
    /// <summary>Số em chưa bắt đầu làm bài, đã bị gỡ khỏi bài này.</summary>
    public required int RevokedCount { get; init; }

    /// <summary>Số em đã bắt đầu làm bài nên vẫn giữ bài (không thu hồi được).</summary>
    public required int KeptStartedCount { get; init; }
}
