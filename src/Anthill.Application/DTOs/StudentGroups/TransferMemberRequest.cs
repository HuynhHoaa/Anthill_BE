namespace Anthill.Application.DTOs.StudentGroups;

/// <summary>
/// Thân request của <c>POST /api/student-groups/{id}/students/{studentId}/transfer</c> — chuyển một học sinh từ
/// lớp <c>{id}</c> sang lớp khác.
/// </summary>
/// <remarks>
/// Chuyển là gỡ em khỏi lớp {id} và xếp vào lớp đích trong một bước. Em học được nhiều lớp cùng lúc nên không còn khoá
/// "đã làm bài thì không chuyển"; việc gỡ vẫn bắt buộc lý do và được lưu vào lịch sử của học sinh.
/// </remarks>
public sealed record TransferMemberRequest
{
    /// <summary>Lớp đích. Phải khác lớp {id}, em chưa học ở đó, và lớp chưa bị khoá.</summary>
    public required int TargetGroupId { get; init; }

    /// <summary>10–512 ký tự sau khi cắt khoảng trắng hai đầu.</summary>
    public required string Reason { get; init; }
}
