using Anthill.Application.DTOs.ExamAttempts;

namespace Anthill.Application.Common;

/// <summary>Đọc tập phương án đã chọn từ một lựa chọn của học sinh.</summary>
public static class AnswerSelectionExtensions
{
    /// <summary>
    /// Các id phương án học sinh đã chọn, không trùng, theo thứ tự gửi lên. Rỗng là bỏ trống câu.
    /// </summary>
    /// <remarks>
    /// Hợp nhất hai cách gửi: <c>answerIds</c> (tập, dùng cho mọi câu) và <c>answerId</c> (một ô hoặc NULL,
    /// cách cũ của câu một đáp án đúng). Validator đã chặn việc gửi cả hai, nên ở đây chỉ có một bên có giá trị.
    /// </remarks>
    public static IReadOnlyList<int> SelectedIds(this AnswerSelectionDto selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        if (selection.AnswerIds is { } ids)
        {
            return ids.Distinct().ToList();
        }

        return selection.AnswerId is { } id ? [id] : [];
    }
}
