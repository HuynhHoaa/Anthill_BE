namespace EduQuiz.Application.DTOs.StudentGroups;

/// <summary>Kết quả phân công giáo viên theo lô.</summary>
/// <remarks>
/// Tách từng lý do bị bỏ qua vì mỗi lý do đòi một hành động khác nhau: người đã được phân công thì
/// không cần làm gì, id không phải giáo viên là chọn nhầm, còn tài khoản bị khoá thì phải mở khoá
/// trước. Gộp thành một con số là lấy mất đúng thông tin người dùng cần.
/// </remarks>
public sealed record AssignTeachersResponse
{
    public required int Assigned { get; init; }

    public required int SkippedAlreadyAssigned { get; init; }

    /// <summary>Id không tồn tại, hoặc tồn tại nhưng không phải tài khoản giáo viên.</summary>
    public required int SkippedNotTeacher { get; init; }

    public required int SkippedLocked { get; init; }

    /// <summary>Tổng số giáo viên đang phụ trách lớp sau thao tác này.</summary>
    public required int TeacherCount { get; init; }
}
