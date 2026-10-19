namespace Anthill.Application.DTOs.StudentGroups;

/// <summary>
/// Kết quả của việc gỡ khỏi lớp hay chuyển lớp: bao nhiêu bài đã giao qua lớp cũ bị thu hồi khỏi em, bao nhiêu
/// bài được giữ lại.
/// </summary>
/// <remarks>
/// Em rời lớp thì những bài lớp cũ giao mà em chưa bắt đầu và chưa hết hạn không còn thuộc về em nữa — nếu giữ,
/// em vẫn vào thi được bài của một lớp em không còn học. Bài em đã bắt đầu và bài đã hết hạn mà em không làm thì
/// được giữ nguyên: một là kết quả đã có, hai là bằng chứng em đã vắng mặt.
/// </remarks>
public sealed record MemberChangeResponse
{
    /// <summary>Số bài chưa bắt đầu, chưa hết hạn của lớp cũ đã bị thu hồi khỏi em.</summary>
    public required int RevokedExams { get; init; }

    /// <summary>Số bài của lớp cũ được giữ lại (đã bắt đầu, hoặc đã hết hạn mà em không làm).</summary>
    public required int KeptExams { get; init; }
}
