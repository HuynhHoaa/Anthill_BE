namespace EduQuiz.Application.DTOs.ExamAttempts;

/// <summary>
/// Kết quả một lần lưu nháp — nói rõ gói vừa gửi có được ghi hay không.
/// </summary>
/// <remarks>
/// Trước đây endpoint này chỉ trả một câu "Đã lưu bài làm." cho cả hai trường hợp. Nhưng gói đến
/// sai thứ tự bị bỏ qua mà vẫn nhận đúng câu đó, nên client không có cách nào phân biệt "đã ghi"
/// với "bị bỏ". Nếu bộ đếm phía client lệch — sau khi tải lại trang chẳng hạn — thì mọi lần lưu
/// đều rơi vào nhánh bỏ qua và giao diện vẫn hiện "đã lưu lúc 10:32", trong khi database không hề
/// có gì.
///
/// Trả về số thứ tự server đang giữ cho phép client tự phát hiện và chỉnh lại bộ đếm của mình.
/// </remarks>
public sealed record SaveAnswersResponse
{
    /// <summary><c>false</c> nghĩa là gói bị bỏ qua vì cũ hơn thứ server đã ghi.</summary>
    public required bool Applied { get; init; }

    /// <summary>Số thứ tự cuối cùng server đã áp dụng. Client phải gửi số lớn hơn giá trị này.</summary>
    public required long LastAppliedSeq { get; init; }

    /// <summary>Số câu trả lời thực sự được ghi trong lần gọi này.</summary>
    public required int SavedCount { get; init; }
}
