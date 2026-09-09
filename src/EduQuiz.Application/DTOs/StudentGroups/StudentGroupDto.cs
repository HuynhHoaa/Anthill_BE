namespace EduQuiz.Application.DTOs.StudentGroups;

/// <summary>
/// Một nhóm lớp. Khớp từng trường với interface <c>StudentGroup</c> của frontend.
/// </summary>
/// <remarks>
/// <see cref="StudentCount"/> luôn đếm qua JOIN chứ không lưu thành cột (§5). Lưu cứng thì mọi
/// đường làm sĩ số thay đổi — tạo học sinh, sửa nhóm của học sinh, thêm/gỡ thành viên, xoá tài
/// khoản — đều phải nhớ cộng trừ lại; sót một đường là con số sai vĩnh viễn mà không ai biết.
/// Frontend hiện đúng vì có hàm <c>syncGroupCounts()</c> chạy sau mỗi thao tác, và đó chính là
/// thứ không cần tồn tại nữa khi dữ liệu đã chuẩn hoá.
/// </remarks>
public sealed record StudentGroupDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Chuỗi rỗng khi không có mô tả — frontend khai báo trường này không nullable.</summary>
    public required string Description { get; init; }

    /// <summary>Sĩ số hiện tại, đếm qua JOIN.</summary>
    public required int StudentCount { get; init; }

    /// <summary>ISO 8601 UTC khi serialize, đúng quy ước §1.</summary>
    public required DateTime CreatedAt { get; init; }
}
