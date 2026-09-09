namespace EduQuiz.Application.DTOs.Questions;

/// <summary>
/// Thân request của <c>POST</c> và <c>PUT /api/questions</c>.
/// </summary>
/// <remarks>
/// <para>
/// Một DTO cho cả tạo lẫn sửa vì <c>PUT</c> ở đây là <b>thay toàn bộ</b>, kể cả mảng đáp án (§4.5).
/// Không có dạng "sửa một phần" nào cả — chính điều đó bảo đảm không tồn tại đường nào sửa lẻ một
/// đáp án của câu hỏi đang nằm trong đề.
/// </para>
/// <para>
/// Cố ý không có <c>CreatedByUserId</c>: người soạn suy từ JWT lúc tạo, và <b>không đổi</b> khi
/// sửa — người sửa không cướp quyền tác giả của người soạn.
/// </para>
/// </remarks>
public sealed record SaveQuestionRequest
{
    public required string Content { get; init; }

    /// <summary>URL trả về từ <c>POST /api/uploads/images</c>, không phải data URI (§1).</summary>
    public string? ImageUrl { get; init; }

    public required string Topic { get; init; }

    /// <summary><c>"easy"</c>, <c>"medium"</c> hoặc <c>"hard"</c>.</summary>
    public required string Difficulty { get; init; }

    public required decimal DefaultPoint { get; init; }

    /// <summary><c>"active"</c> hoặc <c>"hidden"</c>. Bỏ trống thì mặc định <c>"active"</c> lúc tạo, giữ nguyên lúc sửa.</summary>
    public string? Status { get; init; }

    /// <summary>
    /// <c>"shared"</c> (mặc định) hoặc <c>"private"</c>. <c>Private</c> để người soạn giữ lại phần
    /// đề thi thật mà vẫn đóng góp phần còn lại vào ngân hàng chung — rủi ro thật của ngân hàng
    /// dùng chung là <b>lộ đề</b> giữa hai lớp học cùng buổi (§6).
    /// </summary>
    public string? Visibility { get; init; }

    /// <summary>
    /// Môn của câu hỏi. <b>Giáo viên bỏ trống</b> — server lấy từ claim <c>subject_id</c>, đúng như
    /// nhóm lớp lấy chủ sở hữu từ JWT. Trường này tồn tại cho <b>quản trị viên</b>, vì tài khoản
    /// quản trị không thuộc môn nào nên không có gì để suy ra; với họ đây là trường bắt buộc.
    /// </summary>
    public int? SubjectId { get; init; }

    /// <summary>Ít nhất hai đáp án, và đúng <b>một</b> đáp án đúng. Thứ tự phần tử là thứ tự hiển thị.</summary>
    public required IReadOnlyList<SaveAnswerOptionRequest> Answers { get; init; }
}
