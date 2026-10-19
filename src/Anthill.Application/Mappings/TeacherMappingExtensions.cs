using Anthill.Application.DTOs.Teachers;
using Anthill.Domain.Entities;

namespace Anthill.Application.Mappings;

/// <summary>Ánh xạ tài khoản giáo viên sang <see cref="TeacherDto"/>.</summary>
public static class TeacherMappingExtensions
{
    /// <param name="teacher">Tài khoản giáo viên, nạp kèm <c>Subject</c>.</param>
    /// <param name="groupCount">Số lớp đang phụ trách.</param>
    /// <param name="groups">Chi tiết các lớp; để NULL ở danh sách.</param>
    public static TeacherDto ToTeacherDto(
        this User teacher,
        int groupCount,
        IReadOnlyList<TeacherGroupDto>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(teacher);

        return new TeacherDto
        {
            Id = teacher.Id,
            FullName = teacher.FullName,
            Email = teacher.Email,
            Phone = teacher.Phone,
            SubjectId = teacher.SubjectId,
            Subject = teacher.Subject?.Name ?? string.Empty,
            Status = teacher.Status.ToStatusString(),
            AvatarColor = teacher.AvatarColor,
            AvatarUrl = teacher.AvatarUrl,
            MustChangePassword = teacher.MustChangePassword,
            GroupCount = groupCount,
            CreatedAt = teacher.CreatedAt.ToUtc(),
            Groups = groups
        };
    }
}
