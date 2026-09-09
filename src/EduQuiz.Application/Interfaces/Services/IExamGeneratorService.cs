namespace EduQuiz.Application.Interfaces.Services;

/// <summary>
/// Sinh đề từ blueprint — trái tim của đề tài, và là chỗ cờ <c>randomizePerStudent</c>
/// từ điều khiển giả trở thành hành vi thật (§5).
/// </summary>
/// <remarks>
/// <b>Bản đồ đường đi</b> (chờ <c>DTOs/Exams/</c>):
/// <code>
/// Task&lt;Result&lt;ExamPaper&gt;&gt; GetOrCreatePaperAsync(int examId, int assignmentId, CancellationToken ct)
/// Task&lt;Result&lt;BlueprintPreviewDto&gt;&gt; PreviewAsync(BlueprintInput input, int subjectId, int currentUserId, CancellationToken ct)
/// IReadOnlyDictionary&lt;QuestionDifficulty, int&gt; CalculateRequiredCounts(int totalQuestions, int easy, int medium, int hard)
/// </code>
/// Ràng buộc phải cài (§5):
/// <list type="bullet">
/// <item><b>Hạt giống.</b> Tắt <c>randomizePerStudent</c>: một bộ câu duy nhất cho cả lớp, seed
/// <c>hash(examId)</c>, lưu thành <c>ExamPaper</c> có <c>AssignmentId = NULL</c>.
/// Bật: mỗi học sinh một bộ riêng, seed <c>hash($"{examId}:{assignmentId}")</c>.
/// Seed theo <b>assignmentId</b> chứ không phải attemptId — học sinh F5 hoặc mất mạng rồi vào
/// lại phải thấy đúng bộ câu cũ.</item>
/// <item><c>shuffleQuestions</c> và <c>shuffleAnswers</c> nằm <b>trong cùng bước sinh đề</b>,
/// dùng chung một nguồn ngẫu nhiên đã seed (Fisher-Yates), rồi lưu <c>Ordinal</c> xuống DB.
/// Tuyệt đối không xáo lại ở tầng trả response.</item>
/// <item><b>Snapshot</b> <c>content</c>, <c>imageUrl</c>, <c>point</c> và toàn bộ đáp án tại thời
/// điểm sinh đề. Giáo viên sửa hoặc xoá câu hỏi sau đó không được làm đổi nội dung hay điểm của
/// bài đã chấm — mock hiện tra ngược <c>mockQuestions</c> theo id lúc chấm, tức là sửa một câu
/// hỏi làm sai lệch điểm các bài đã chấm trước đó.</item>
/// <item>Bốc câu bằng Fisher-Yates có seed trên tập ứng viên đọc sẵn, <b>không</b> dùng
/// <c>ORDER BY RANDOM()</c> — cùng seed phải cho ra cùng kết quả.</item>
/// </list>
/// </remarks>
public interface IExamGeneratorService
{
}
