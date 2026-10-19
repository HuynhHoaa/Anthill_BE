using Anthill.Application.DTOs.StudentGroups;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Mappings;

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
            IsLocked = group.IsLocked,
            LockedAt = group.LockedAt?.ToUtc(),

            // Mảng rỗng khi bảng phân công chưa được nạp trông hệt như "chưa có giáo viên nào" —
            // mà đó lại là tín hiệu cảnh báo trên giao diện quản trị. Nên mọi truy vấn phục vụ
            // danh sách lớp đều Include sẵn Teachers; xem StudentGroupRepository.
            Teachers = group.Teachers
                .Where(assignment => assignment.Teacher is not null)
                .Select(assignment => assignment.Teacher.ToGroupTeacherDto())
                .OrderBy(teacher => teacher.FullName, StringComparer.CurrentCulture)
                .ToList(),

            CreatedAt = group.CreatedAt.ToUtc(),
            TermId = group.AcademicTermId,
            TermName = group.AcademicTerm?.Name
        };
    }

    /// <summary>Một dòng lịch sử của lớp; <paramref name="change"/> nạp kèm <c>Actor</c>.</summary>
    public static GroupChangeDto ToDto(this GroupChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        return new GroupChangeDto
        {
            Id = change.Id,
            Kind = change.Kind switch
            {
                GroupChangeKind.Created => "created",
                GroupChangeKind.TeacherChanged => "teacherChanged",
                GroupChangeKind.Locked => "locked",
                GroupChangeKind.Unlocked => "unlocked",
                _ => throw new ArgumentOutOfRangeException(nameof(change), change.Kind, "Loại thay đổi của lớp không hợp lệ.")
            },
            Reason = change.Reason,
            FromTeacher = change.FromTeacherName,
            ToTeacher = change.ToTeacherName,
            ActorName = change.Actor?.FullName,
            CreatedAt = change.CreatedAt.ToUtc()
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
