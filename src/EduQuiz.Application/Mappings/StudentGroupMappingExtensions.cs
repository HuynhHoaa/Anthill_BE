using EduQuiz.Application.DTOs.StudentGroups;
using EduQuiz.Domain.Entities;

namespace EduQuiz.Application.Mappings;

/// <summary>Ánh xạ <see cref="StudentGroup"/> sang DTO.</summary>
public static class StudentGroupMappingExtensions
{
    /// <summary>
    /// Dựng DTO nhóm lớp.
    /// </summary>
    /// <param name="group">Nhóm đọc từ database.</param>
    /// <param name="studentCount">
    /// Sĩ số đã đếm sẵn. Tham số bắt buộc chứ không đọc từ <c>group.Students.Count</c>: navigation
    /// đó rỗng ở mọi truy vấn không <c>Include</c>, và đọc nhầm nó thì màn hình danh sách hiện sĩ
    /// số 0 cho mọi nhóm mà không có lỗi nào xảy ra. Bắt truyền vào buộc chỗ gọi phải quyết định
    /// lấy con số ở đâu — với danh sách thì là một truy vấn <c>GROUP BY</c> duy nhất
    /// (<c>CountStudentsByGroupAsync</c>), không phải N+1.
    /// </param>
    /// <remarks>
    /// <c>OwnerTeacherId</c> cố ý không xuất hiện trong DTO: giáo viên chỉ nhìn thấy nhóm của
    /// chính mình nên trường đó không nói thêm điều gì, còn với quản trị viên thì màn hình đang
    /// không có cột nào để hiển thị nó.
    /// </remarks>
    public static StudentGroupDto ToDto(this StudentGroup group, int studentCount)
    {
        ArgumentNullException.ThrowIfNull(group);

        return new StudentGroupDto
        {
            Id = group.Id,
            Name = group.Name,

            // Frontend khai báo description: string không nullable và hiển thị thẳng lên bảng.
            Description = group.Description ?? string.Empty,

            StudentCount = studentCount,
            CreatedAt = group.CreatedAt.ToUtc()
        };
    }
}
