namespace Anthill.Application.DTOs.ExamAttempts;

/// <summary>Thân request của <c>POST /api/exam-attempts</c>.</summary>
/// <remarks>
/// Chỉ đúng một trường. Không có <c>studentId</c> (danh tính lấy từ JWT — §3 quy tắc ④), không có
/// <c>startedAt</c> hay <c>expiresAt</c> (đồng hồ do server chốt — §3 quy tắc ③), và không có
/// <c>examId</c>: bài kiểm tra suy ra từ assignment, còn nhận <c>examId</c> rời thì phải kiểm thêm
/// nó có khớp assignment không, tức là thêm một chỗ để quên kiểm.
/// </remarks>
public sealed record StartAttemptRequest
{
    public required int AssignmentId { get; init; }
}
