using EduQuiz.Application.DTOs.Questions;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Mappings;

/// <summary>
/// Ánh xạ <see cref="Question"/> sang DTO, cùng ba cặp đọc/ghi chuỗi enum của ngân hàng câu hỏi.
/// </summary>
/// <remarks>
/// Ba enum ở đây đều phải xuống chữ thường cho frontend, cùng lý do đã ghi ở
/// <see cref="UserMappingExtensions.ToRoleString"/>: frontend khai báo union chữ thường và so sánh
/// trực tiếp, nên trả về <c>"Easy"</c> theo tên enum .NET là làm hỏng mọi bộ lọc.
/// </remarks>
public static class QuestionMappingExtensions
{
    /// <summary>Giá trị frontend gửi khi ô lọc đang để "tất cả".</summary>
    public const string AllFilterValue = "all";

    /// <summary>
    /// Dựng DTO câu hỏi.
    /// </summary>
    /// <param name="question">Câu hỏi đã nạp kèm <c>Answers</c> và <c>CreatedBy</c>.</param>
    /// <param name="canEdit">
    /// Người đang đăng nhập có sửa được câu này không. Tham số bắt buộc chứ không tự suy trong đây:
    /// quy tắc thật là <c>CreatedByUserId == currentUserId || Role == Admin</c>, mà lớp mapping thì
    /// không biết gì về người đang gọi. Truyền vào buộc chỗ gọi phải trả lời câu hỏi đó một cách
    /// có ý thức.
    /// </param>
    public static QuestionDto ToDto(this Question question, bool canEdit)
    {
        ArgumentNullException.ThrowIfNull(question);

        return new QuestionDto
        {
            Id = question.Id,
            Content = question.Content,
            ImageUrl = question.ImageUrl,
            Topic = question.Topic,
            Difficulty = question.Difficulty.ToDifficultyString(),
            DefaultPoint = question.DefaultPoint,
            Status = question.Status.ToQuestionStatusString(),

            // Frontend khai báo updatedAt không nullable và hiển thị thẳng lên cột "Cập nhật";
            // câu chưa từng sửa thì ngày tạo là câu trả lời đúng, không phải ô trống.
            UpdatedAt = (question.UpdatedAt ?? question.CreatedAt).ToUtc(),

            Answers = question.Answers
                .OrderBy(answer => answer.Ordinal)
                .Select(answer => new AnswerOptionDto
                {
                    Id = answer.Id,
                    Content = answer.Content,
                    ImageUrl = answer.ImageUrl,
                    IsCorrect = answer.IsCorrect
                })
                .ToList(),

            // Navigation có thể chưa được nạp ở đường ghi (entity trần do DbContext theo dõi);
            // chuỗi rỗng thay vì ném lỗi, vì một cột hiển thị trống không đáng làm hỏng cả response.
            CreatedBy = question.CreatedBy?.FullName ?? string.Empty,
            CreatedByUserId = question.CreatedByUserId,
            Visibility = question.Visibility.ToVisibilityString(),
            CanEdit = canEdit
        };
    }

    public static string ToDifficultyString(this QuestionDifficulty difficulty) => difficulty switch
    {
        QuestionDifficulty.Easy => "easy",
        QuestionDifficulty.Medium => "medium",
        QuestionDifficulty.Hard => "hard",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Mức độ khó không hợp lệ.")
    };

    public static string ToQuestionStatusString(this QuestionStatus status) => status switch
    {
        QuestionStatus.Active => "active",
        QuestionStatus.Hidden => "hidden",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái câu hỏi không hợp lệ.")
    };

    public static string ToVisibilityString(this QuestionVisibility visibility) => visibility switch
    {
        QuestionVisibility.Shared => "shared",
        QuestionVisibility.Private => "private",
        _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Phạm vi chia sẻ không hợp lệ.")
    };

    public static bool TryParseDifficulty(string? value, out QuestionDifficulty difficulty) =>
        TryParseEnum(value, out difficulty);

    public static bool TryParseQuestionStatus(string? value, out QuestionStatus status) =>
        TryParseEnum(value, out status);

    public static bool TryParseVisibility(string? value, out QuestionVisibility visibility) =>
        TryParseEnum(value, out visibility);

    /// <summary>Ô lọc mức độ: chấp nhận thêm <c>"all"</c> và chuỗi rỗng, cả hai nghĩa là không lọc.</summary>
    /// <returns>False khi giá trị không đọc được — chỗ gọi trả lỗi thay vì âm thầm bỏ lọc.</returns>
    public static bool TryParseDifficultyFilter(string? value, out QuestionDifficulty? difficulty) =>
        TryParseFilter(value, out difficulty);

    /// <summary>Ô lọc trạng thái, cùng quy ước với <see cref="TryParseDifficultyFilter"/>.</summary>
    public static bool TryParseQuestionStatusFilter(string? value, out QuestionStatus? status) =>
        TryParseFilter(value, out status);

    /// <summary>Ô lọc chủ đề: <c>"all"</c> và chuỗi rỗng đều thành NULL, ngoài ra trả về chuỗi đã trim.</summary>
    public static string? NormalizeTopicFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return string.Equals(trimmed, AllFilterValue, StringComparison.OrdinalIgnoreCase) ? null : trimmed;
    }

    /// <summary>
    /// <c>Enum.IsDefined</c> là phần không được bỏ: nếu không, client gửi <c>"7"</c> sẽ parse thành
    /// công thành một giá trị enum không tồn tại rồi đi thẳng xuống database.
    /// </summary>
    private static bool TryParseEnum<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out result) && Enum.IsDefined(result);
    }

    private static bool TryParseFilter<TEnum>(string? value, out TEnum? result) where TEnum : struct, Enum
    {
        result = null;

        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value.Trim(), AllFilterValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!TryParseEnum<TEnum>(value, out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }
}
