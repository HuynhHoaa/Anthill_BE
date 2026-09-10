using EduQuiz.Domain.Entities;

namespace EduQuiz.Application.Common;

/// <summary>
/// Lịch có hiệu lực của một bản ghi giao bài.
/// </summary>
/// <remarks>
/// <para>
/// Một đề thường được dùng cho nhiều lớp mà mỗi lớp học vào một tiết khác nhau, nên lịch của đề
/// chỉ là <b>mặc định</b>; từng lượt giao có thể đặt giờ riêng. Kể từ khi có điều đó, đọc thẳng
/// <c>exam.OpenAt</c> ở phía học sinh là sai — và sai theo cách tệ nhất: em vào được bài ngoài giờ
/// lớp mình, hoặc bị chặn trong chính giờ của mình.
/// </para>
/// <para>
/// Vì vậy mọi nơi tính giờ cho học sinh phải đi qua đây. Đây là một hàm thuần, không chạm database,
/// nên kiểm được bằng unit test — và nó được kiểm, vì đây là loại lỗi chỉ lộ ra đúng lúc thi.
/// </para>
/// </remarks>
public static class AssignmentSchedule
{
    /// <summary>Giờ mở và giờ đóng thật sự áp cho bản ghi này.</summary>
    /// <exception cref="InvalidOperationException">
    /// Ném khi <c>Exam</c> chưa được nạp và bản ghi lại không có giờ riêng — im lặng trả về
    /// <c>default(DateTime)</c> ở đây sẽ thành năm 0001, tức mọi bài đều "đã đóng từ lâu".
    /// </exception>
    public static (DateTime OpenAt, DateTime CloseAt) Resolve(ExamAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (assignment.OpenAt is { } openAt && assignment.CloseAt is { } closeAt)
        {
            return (openAt, closeAt);
        }

        if (assignment.Exam is null)
        {
            throw new InvalidOperationException(
                "Bản ghi giao bài không có giờ riêng nhưng Exam chưa được nạp — không tính được lịch.");
        }

        // Hai trường đi cùng cặp: đặt một nửa là trạng thái vô nghĩa, nên chỉ cần một trong hai
        // vắng mặt là quay về lịch chung của đề, thay vì ghép nửa nọ với nửa kia.
        return (assignment.OpenAt ?? assignment.Exam.OpenAt, assignment.CloseAt ?? assignment.Exam.CloseAt);
    }

    /// <summary>Bản ghi này có đặt giờ riêng hay đang theo lịch chung của đề.</summary>
    public static bool HasOverride(ExamAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        return assignment.OpenAt.HasValue && assignment.CloseAt.HasValue;
    }
}
