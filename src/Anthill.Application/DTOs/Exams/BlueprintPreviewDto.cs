namespace Anthill.Application.DTOs.Exams;

/// <summary>
/// Kết quả đối chiếu blueprint với tồn kho ngân hàng câu hỏi — màn hình ra đề gọi mỗi lần
/// giáo viên kéo thanh trượt.
/// </summary>
/// <remarks>
/// Ba khoá của hai từ điển là <c>"easy"</c>, <c>"medium"</c>, <c>"hard"</c> — viết thường, khớp
/// <c>Record&lt;DifficultyLevel, number&gt;</c> mà frontend khai báo.
/// </remarks>
public sealed record BlueprintPreviewDto
{
    /// <summary>Số câu <b>cần</b> cho từng mức, do backend tính từ tỷ lệ.</summary>
    public required IReadOnlyDictionary<string, int> Required { get; init; }

    /// <summary>Số câu <b>đang có</b> trong ngân hàng ở từng mức, đã lọc theo chủ đề và phạm vi môn.</summary>
    public required IReadOnlyDictionary<string, int> Available { get; init; }

    /// <summary>Đủ điều kiện sinh đề hay chưa. Đúng bằng <c>Errors.Count == 0</c>.</summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Câu chữ hiện thẳng lên màn hình, mỗi câu một lỗi. Trả về danh sách chứ không phải một
    /// chuỗi gộp, để giao diện tự chọn cách trình bày.
    /// </summary>
    public required IReadOnlyList<string> Errors { get; init; }
}
