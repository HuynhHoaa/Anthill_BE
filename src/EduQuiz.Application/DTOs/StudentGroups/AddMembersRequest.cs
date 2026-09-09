namespace EduQuiz.Application.DTOs.StudentGroups;

/// <summary>
/// Thân request của <c>POST /api/student-groups/{id}/students</c> — thêm <b>nhiều</b> học sinh
/// trong một lần gọi (§4.4).
/// </summary>
/// <remarks>
/// Frontend hiện gọi lặp từng em rồi tự đếm thành công/thất bại. Ngoài chuyện N request, cách đó
/// còn để lại trạng thái nửa vời khi mạng đứt giữa chừng: mười em đã vào nhóm, mười em còn lại
/// thì không, và không có cách nào lùi lại. Gộp thành một lần gọi thì một lần
/// <c>SaveChangesAsync</c> quyết định tất cả cùng vào hoặc không em nào vào.
/// </remarks>
public sealed record AddMembersRequest
{
    /// <summary>Id trùng nhau trong mảng được khử trước khi xử lý.</summary>
    public required IReadOnlyList<int> StudentIds { get; init; }
}
