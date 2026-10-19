using Anthill.Application.DTOs.Questions;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Mappings;

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
    public const string AllFilterValue = FilterValueParser.AllFilterValue;

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
            Title = question.Title,
            Content = question.Content,
            ImageUrl = question.ImageUrl,
            Topic = question.Topic,
            Difficulty = question.Difficulty.ToDifficultyString(),
            AllowMultiple = question.AllowMultiple,
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
            SubjectId = question.SubjectId,
            Subject = question.Subject?.Name ?? string.Empty,
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
        FilterValueParser.TryParseEnum(value, out difficulty);

    public static bool TryParseQuestionStatus(string? value, out QuestionStatus status) =>
        FilterValueParser.TryParseEnum(value, out status);

    public static bool TryParseVisibility(string? value, out QuestionVisibility visibility) =>
        FilterValueParser.TryParseEnum(value, out visibility);

    /// <summary>Ô lọc mức độ: chấp nhận thêm <c>"all"</c> và chuỗi rỗng, cả hai nghĩa là không lọc.</summary>
    /// <returns>False khi giá trị không đọc được — chỗ gọi trả lỗi thay vì âm thầm bỏ lọc.</returns>
    public static bool TryParseDifficultyFilter(string? value, out QuestionDifficulty? difficulty) =>
        FilterValueParser.TryParseFilter(value, out difficulty);

    /// <summary>Ô lọc trạng thái, cùng quy ước với <see cref="TryParseDifficultyFilter"/>.</summary>
    public static bool TryParseQuestionStatusFilter(string? value, out QuestionStatus? status) =>
        FilterValueParser.TryParseFilter(value, out status);

    /// <summary>Ô lọc chủ đề: <c>"all"</c> và chuỗi rỗng đều thành NULL, ngoài ra trả về chuỗi đã trim.</summary>
    public static string? NormalizeTopicFilter(string? value) =>
        FilterValueParser.NormalizeTextFilter(value);

    /// <summary>
    /// Tên câu hỏi đã chuẩn hoá: bỏ khoảng trắng hai đầu và gộp mọi dãy khoảng trắng (kể cả xuống dòng) thành
    /// một dấu cách. Tên là một dòng; một lời gọi API dán cả đoạn nhiều dòng vào đây không được làm vỡ hàng
    /// trong danh sách.
    /// </summary>
    /// <returns>Chuỗi rỗng khi chưa đặt tên — không bao giờ null.</returns>
    public static string NormalizeTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
