using Anthill.Application.DTOs.Questions;

namespace Anthill.Application.DTOs.Exams;

/// <summary>Kết quả soạn tự động: các câu rút được, và số câu còn thiếu ở từng mức.</summary>
/// <remarks>
/// Thiếu câu không phải lỗi: rút được bao nhiêu trả bấy nhiêu, và <see cref="Missing"/> nói mức nào chưa đủ
/// để giáo viên thêm tay hoặc bổ sung ngân hàng. Không ghi gì xuống database — đề chỉ đổi khi giáo viên lưu.
/// </remarks>
public sealed record ComposeQuestionsResultDto
{
    public required IReadOnlyList<QuestionDto> Questions { get; init; }

    /// <summary>Khoá <c>easy</c>/<c>medium</c>/<c>hard</c>; giá trị 0 nghĩa là đủ.</summary>
    public required IReadOnlyDictionary<string, int> Missing { get; init; }

    /// <summary>
    /// Số câu đủ điều kiện (đúng chủ đề, mức độ, chưa nằm trong đề) nhưng bị bỏ vì các lớp đã chọn đã nhận chúng ở đề trước.
    /// 0 khi không chọn lớp nào. Giáo viên cần số này để hiểu vì sao ngân hàng "hết câu" dù còn nhiều câu trong chủ đề.
    /// </summary>
    public int SkippedAlreadyAssigned { get; init; }
}
