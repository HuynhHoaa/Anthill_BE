namespace Anthill.Application.DTOs.Questions;

/// <summary>
/// Một câu hỏi kèm đáp án. Chín trường đầu khớp từng cái với interface <c>Question</c> của
/// frontend; bốn trường cuối là phần <b>ngân hàng dùng chung</b> mà mock không có khái niệm.
/// </summary>
/// <remarks>
/// <para>
/// Mock của frontend giả định mỗi người có ngân hàng riêng, nên không cần biết ai soạn câu nào.
/// Ở backend, §6 quy định ngân hàng <b>dùng chung theo tổ bộ môn</b>: giáo viên Toán A thấy được
/// câu của giáo viên Toán B nhưng không sửa được. Vì vậy màn hình phải biết hai điều mà trước đây
/// không cần: <i>ai soạn câu này</i> và <i>mình có sửa được không</i>.
/// </para>
/// <para>
/// <see cref="CanEdit"/> được server tính sẵn thay vì để frontend tự suy từ
/// <c>createdByUserId === currentUser.id</c>. Lý do: quy tắc thật là
/// <c>CreatedByUserId == currentUserId || Role == Admin</c>, và mỗi nơi tự suy lại một quy tắc hai
/// vế là cách chắc chắn để hai nơi hiểu khác nhau. Server đã biết câu trả lời rồi thì trả luôn.
/// Đây chỉ là dữ liệu cho giao diện — quyền vẫn thực thi ở tầng service, ẩn nút không phải là chặn.
/// </para>
/// </remarks>
public sealed record QuestionDto
{
    public required int Id { get; init; }

    public required string Content { get; init; }

    public string? ImageUrl { get; init; }

    public required string Topic { get; init; }

    /// <summary>Chữ thường: <c>"easy" | "medium" | "hard"</c>, khớp union <c>DifficultyLevel</c>.</summary>
    public required string Difficulty { get; init; }

    public required decimal DefaultPoint { get; init; }

    /// <summary>Chữ thường: <c>"active" | "hidden"</c>, khớp union <c>QuestionStatus</c>.</summary>
    public required string Status { get; init; }

    /// <summary>
    /// ISO 8601 UTC. Rơi về <c>CreatedAt</c> khi câu hỏi chưa từng được sửa — frontend khai báo
    /// trường này không nullable và hiển thị thẳng lên cột "Cập nhật".
    /// </summary>
    public required DateTime UpdatedAt { get; init; }

    public required IReadOnlyList<AnswerOptionDto> Answers { get; init; }

    /// <summary>Họ tên người soạn, JOIN từ <c>Users</c>. Cột "Soạn bởi" của ngân hàng dùng chung (§6).</summary>
    public required string CreatedBy { get; init; }

    public required int CreatedByUserId { get; init; }

    /// <summary>Chữ thường: <c>"shared" | "private"</c>.</summary>
    public required string Visibility { get; init; }

    /// <summary>Người đang đăng nhập có quyền sửa/xoá câu này không. Dữ liệu cho giao diện, không phải hàng rào.</summary>
    public required bool CanEdit { get; init; }
}
