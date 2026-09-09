namespace EduQuiz.Application.DTOs.StudentGroups;

/// <summary>
/// Kết quả của <c>POST /api/student-groups/{id}/students</c>.
/// </summary>
/// <remarks>
/// <para>
/// Thêm thành viên hàng loạt là thao tác <b>một phần cũng tính là thành công</b>: chọn hai mươi
/// em mà một em vừa bị xoá tài khoản thì mười chín em còn lại vẫn nên vào nhóm. Vì vậy endpoint
/// trả 200 kèm bảng phân loại dưới đây, thay vì 400 làm hỏng cả mẻ vì một id lạc.
/// </para>
/// <para>
/// Ba con số tách riêng chứ không gộp thành "thất bại" vì chúng dẫn tới ba câu khác nhau trên
/// màn hình: em đã ở sẵn trong nhóm là chuyện bình thường, còn id không tồn tại thì danh sách
/// giáo viên đang nhìn đã cũ và nên tải lại.
/// </para>
/// </remarks>
public sealed record AddMembersResponse
{
    /// <summary>Số học sinh thật sự được chuyển vào nhóm ở lần gọi này.</summary>
    public required int Added { get; init; }

    /// <summary>Đã thuộc nhóm này từ trước — bỏ qua, không tính là lỗi.</summary>
    public required int SkippedAlreadyMember { get; init; }

    /// <summary>Id không tồn tại hoặc không phải tài khoản học sinh.</summary>
    public required int SkippedNotFound { get; init; }

    /// <summary>Sĩ số sau khi thêm, để màn hình cập nhật mà không phải gọi lại danh sách.</summary>
    public required int StudentCount { get; init; }
}
