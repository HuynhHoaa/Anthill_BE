namespace Anthill.Application.DTOs.Exams;

/// <summary>
/// Thân của <c>POST /api/exams/compose-questions</c>: "soạn tự động" một nhóm câu hỏi theo số câu từng mức.
/// </summary>
public sealed record ComposeQuestionsRequest
{
    public int Easy { get; init; }

    public int Medium { get; init; }

    public int Hard { get; init; }

    /// <summary>Giới hạn theo chủ đề. Bỏ trống hoặc <c>"all"</c> nghĩa là mọi chủ đề.</summary>
    public string? Topic { get; init; }

    /// <summary>Câu đã có trong đề, để không rút trùng.</summary>
    public IReadOnlyList<int>? ExcludeQuestionIds { get; init; }

    /// <summary>
    /// Bỏ những câu mà học sinh của các lớp này đã nhận trong các đề đã giao trước đây, để giao đề mới không lặp lại câu cũ.
    /// Chỉ nhận lớp giáo viên được phân công và đã công bố; lớp khác trả <b>400</b>.
    /// </summary>
    public IReadOnlyList<int>? ExcludeAssignedToGroupIds { get; init; }
}
