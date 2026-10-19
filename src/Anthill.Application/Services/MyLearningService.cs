using Anthill.Application.Common;
using Anthill.Application.DTOs.MyLearning;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;

namespace Anthill.Application.Services;

/// <summary>
/// Trang "Lớp học của tôi" và "Bảng điểm" của học sinh — xem <see cref="IMyLearningService"/>.
/// </summary>
/// <remarks>
/// Mọi con số đi qua <c>ToStudentExamItemDto</c>, cùng hàm dựng thẻ bài của <c>GET /api/my/exams</c>: điểm chỉ có mặt khi giáo viên cho em
/// xem, nên bảng điểm không có đường vòng nào để đọc điểm bị giấu.
/// </remarks>
public sealed class MyLearningService : IMyLearningService
{
    private const string GroupNotFoundMessage = "Không tìm thấy lớp này trong danh sách lớp của bạn.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public MyLearningService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    private int StudentId => _currentUser.RequireUserId();

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<MyGroupDto>>> ListMyGroupsAsync(CancellationToken cancellationToken = default)
    {
        var groups = await LoadMyGroupsAsync(cancellationToken);

        return Result.Success<IReadOnlyList<MyGroupDto>>(groups);
    }

    /// <inheritdoc />
    public async Task<Result<MyGroupDto>> GetMyGroupAsync(int groupId, CancellationToken cancellationToken = default)
    {
        var group = (await LoadMyGroupsAsync(cancellationToken)).FirstOrDefault(item => item.Id == groupId);

        return group is null
            ? Result.NotFound<MyGroupDto>(GroupNotFoundMessage)
            : Result.Success(group);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<MyTermDto>>> ListMyTermsAsync(CancellationToken cancellationToken = default)
    {
        var groups = await LoadMyGroupsAsync(cancellationToken);
        var countByTerm = groups
            .Where(group => group.TermId is not null)
            .GroupBy(group => group.TermId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        var terms = (await _unitOfWork.AcademicTerms.GetAllOrderedAsync(cancellationToken))
            .Where(term => countByTerm.ContainsKey(term.Id))
            .Select(term => new MyTermDto
            {
                Id = term.Id,
                Name = term.Name,
                StartsOn = term.StartsOn,
                EndsOn = term.EndsOn,
                IsCurrent = term.IsCurrent,
                GroupCount = countByTerm[term.Id]
            })
            .ToList();

        // Lớp chưa phân kỳ là một mục riêng (id 0) ở cuối danh sách: học sinh lọc được như một kỳ.
        var unphased = groups.Count(group => group.TermId is null);
        if (unphased > 0)
        {
            terms.Add(new MyTermDto { Id = 0, Name = "Chưa phân kỳ", IsCurrent = false, GroupCount = unphased });
        }

        return Result.Success<IReadOnlyList<MyTermDto>>(terms);
    }

    /// <inheritdoc />
    public async Task<Result<TranscriptDto>> GetMyTranscriptAsync(int? termId = null, CancellationToken cancellationToken = default)
    {
        var asOf = DateTime.UtcNow;
        var assignments = (await _unitOfWork.ExamAssignments.GetForStudentAsync(StudentId, cancellationToken))
            .Where(assignment => termId is null
                || (termId == 0 ? assignment.AssignedViaGroup?.AcademicTermId is null : assignment.AssignedViaGroup?.AcademicTermId == termId))
            .ToList();
        var subjectNameById = (await _unitOfWork.Subjects.GetAllAsync(cancellationToken)).ToDictionary(subject => subject.Id, subject => subject.Name);

        var exams = assignments
            .Select(assignment => (
                Item: assignment.ToStudentExamItemDto(asOf),
                SubjectName: assignment.Exam.CreatedBy?.SubjectId is { } subjectId && subjectNameById.TryGetValue(subjectId, out var name) ? name : null))
            .ToList();

        return Result.Success(StudentTranscriptBuilder.Build(exams));
    }

    private async Task<List<MyGroupDto>> LoadMyGroupsAsync(CancellationToken cancellationToken)
    {
        var asOf = DateTime.UtcNow;

        // Lớp nháp là lớp quản trị viên còn đang chuẩn bị: học sinh chưa thấy, giống giáo viên.
        var groups = (await _unitOfWork.StudentGroups.GetByStudentAsync(StudentId, cancellationToken))
            .Where(group => group.IsPublished)
            .ToList();
        if (groups.Count == 0)
        {
            return [];
        }

        var groupIds = groups.Select(group => group.Id).ToList();
        var studentCounts = await _unitOfWork.StudentGroups.CountMembersAsync(groupIds, cancellationToken);
        var subjectNameById = (await _unitOfWork.Subjects.GetAllAsync(cancellationToken)).ToDictionary(subject => subject.Id, subject => subject.Name);
        var termNameById = (await _unitOfWork.AcademicTerms.GetAllOrderedAsync(cancellationToken)).ToDictionary(term => term.Id, term => term.Name);

        var itemsByGroup = (await _unitOfWork.ExamAssignments.GetForStudentAsync(StudentId, cancellationToken))
            .Where(assignment => assignment.AssignedViaGroupId is { } id && groupIds.Contains(id))
            .Select(assignment => assignment.ToStudentExamItemDto(asOf))
            .GroupBy(item => item.GroupId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var result = new List<MyGroupDto>(groups.Count);
        foreach (var group in groups)
        {
            // Mỗi lớp đúng một giáo viên phụ trách; lớp cũ còn nhiều người thì lấy người đầu theo tên.
            var teacher = (await _unitOfWork.StudentGroups.GetAssignedTeachersAsync(group.Id, cancellationToken)).FirstOrDefault();
            var items = itemsByGroup.GetValueOrDefault(group.Id) ?? [];

            result.Add(new MyGroupDto
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description ?? string.Empty,
                TermId = group.AcademicTermId,
                TermName = group.AcademicTermId is { } termId && termNameById.TryGetValue(termId, out var termName) ? termName : null,
                TeacherName = teacher?.FullName ?? string.Empty,
                SubjectName = teacher?.SubjectId is { } subjectId && subjectNameById.TryGetValue(subjectId, out var name) ? name : null,
                StudentCount = studentCounts.GetValueOrDefault(group.Id, 0),
                IsLocked = group.IsLocked,
                AssignedCount = items.Count,
                CompletedCount = items.Count(item => item.Status is "submitted" or "graded"),
                OpenCount = items.Count(item => item.Status is "assigned" or "inprogress" && item.Lifecycle != "scheduled"),
                UpcomingCount = items.Count(item => item.Status is "assigned" or "inprogress" && item.Lifecycle == "scheduled"),
                ExpiredCount = items.Count(item => item.Status == "expired")
            });
        }

        return result;
    }
}
