using Anthill.Domain.Common;

namespace Anthill.Domain.Entities;

public class AnswerOption : BaseEntity
{
    public int QuestionId { get; set; }

    public required string Content { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>
    /// Không bao giờ rời server trong lúc học sinh làm bài — DTO lúc làm bài
    /// phải là kiểu <i>không khai báo</i> trường này, chứ không phải gán false.
    /// </summary>
    public bool IsCorrect { get; set; }

    public int Ordinal { get; set; }

    public Question Question { get; set; } = null!;
}
