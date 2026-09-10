using EduQuiz.Application.DTOs.StudentGroups;
using EduQuiz.Domain.Entities;

namespace EduQuiz.Application.Mappings;

public static class StudentGroupMappingExtensions
{
    /// <summary>
    /// Sĩ số truyền vào từ ngoài thay vì đọc <c>group.Students.Count</c>.
    /// </summary>
    /// <remarks>
    /// Bộ sưu tập <c>Students</c> chỉ được nạp ở vài truy vấn; đếm trên nó ở những chỗ khác sẽ ra
    /// 0 một cách lặng lẽ. Bắt buộc truyền vào là cách khiến chỗ gọi phải nghĩ xem con số đó từ đâu.
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
            IsPublished = group.IsPublished,
            PublishedAt = group.PublishedAt?.ToUtc(),

            // Mảng rỗng khi bảng phân công chưa được nạp trông hệt như "chưa có giáo viên nào" —
            // mà đó lại là tín hiệu cảnh báo trên giao diện quản trị. Nên mọi truy vấn phục vụ
            // danh sách lớp đều Include sẵn Teachers; xem StudentGroupRepository.
            Teachers = group.Teachers
                .Where(assignment => assignment.Teacher is not null)
                .Select(assignment => assignment.Teacher.ToGroupTeacherDto())
                .OrderBy(teacher => teacher.FullName, StringComparer.CurrentCulture)
                .ToList(),

            CreatedAt = group.CreatedAt.ToUtc()
        };
    }

    public static GroupTeacherDto ToGroupTeacherDto(this User teacher)
    {
        ArgumentNullException.ThrowIfNull(teacher);

        return new GroupTeacherDto
        {
            Id = teacher.Id,
            FullName = teacher.FullName,
            Email = teacher.Email ?? string.Empty,
            Subject = teacher.Subject?.Name ?? string.Empty,
            AvatarColor = teacher.AvatarColor ?? string.Empty,
            Status = teacher.Status.ToStatusString()
        };
    }
}
