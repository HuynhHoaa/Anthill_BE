using Anthill.Application.DTOs.Questions;

namespace Anthill.Application.DTOs.Exams;

/// <summary>
/// Một dòng của danh sách câu hỏi trong đề: câu hỏi đầy đủ (kèm đáp án đúng, để giáo viên duyệt) và điểm
/// của riêng câu đó trong đề này.
/// </summary>
/// <remarks>Chỉ giáo viên chủ đề và quản trị viên đọc được; học sinh không có đường nào tới đây.</remarks>
public sealed record ExamQuestionDto
{
    /// <summary>Thứ tự trong đề, bắt đầu từ 0.</summary>
    public required int Ordinal { get; init; }

    public required decimal Point { get; init; }

    public required QuestionDto Question { get; init; }
}
