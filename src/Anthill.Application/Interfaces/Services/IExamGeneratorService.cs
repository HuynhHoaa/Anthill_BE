using Anthill.Application.Common;
using Anthill.Domain.Entities;

namespace Anthill.Application.Interfaces.Services;

/// <summary>
/// Sinh đề từ blueprint — chỗ cờ <c>randomizePerStudent</c> từ điều khiển giả trở thành hành vi
/// thật (§5).
/// </summary>
/// <remarks>
/// Ràng buộc đã cài:
/// <list type="bullet">
/// <item><b>Hạt giống.</b> Tắt <c>randomizePerStudent</c>: một bộ câu duy nhất cho cả lớp, seed
/// <c>hash(examId)</c>, lưu thành <c>ExamPaper</c> có <c>AssignmentId = NULL</c>. Bật: mỗi học
/// sinh một bộ riêng, seed <c>hash($"{examId}:{assignmentId}")</c>. Seed theo <b>assignmentId</b>
/// chứ không phải attemptId — học sinh F5 hoặc mất mạng rồi vào lại phải thấy đúng bộ câu cũ.</item>
/// <item><c>shuffleQuestions</c> và <c>shuffleAnswers</c> nằm <b>trong cùng bước sinh đề</b>, rồi
/// lưu <c>Ordinal</c> xuống DB. Tuyệt đối không xáo lại ở tầng trả response.</item>
/// <item><b>Snapshot</b> nội dung, ảnh, điểm và toàn bộ đáp án tại thời điểm sinh đề. Giáo viên
/// sửa hoặc xoá câu hỏi sau đó không được làm đổi nội dung hay điểm của bài đã chấm.</item>
/// <item>Bốc câu bằng Fisher-Yates có seed trên tập ứng viên đọc sẵn, <b>không</b> dùng
/// <c>ORDER BY RANDOM()</c>.</item>
/// </list>
/// </remarks>
public interface IExamGeneratorService
{
    /// <summary>
    /// Bản đề của một assignment: trả về bản đã có, hoặc sinh mới nếu chưa có.
    /// </summary>
    /// <remarks>
    /// <b>Không</b> tự lưu — bản đề mới được gắn vào <c>UnitOfWork</c> và người gọi quyết định
    /// thời điểm commit, để việc tạo assignment và sinh đề nằm trong cùng một transaction.
    /// </remarks>
    Task<Result<ExamPaper>> GetOrCreatePaperAsync(
        Exam exam,
        ExamAssignment assignment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sinh bản đề cho một loạt assignment: bản chung một lần cho cả loạt, hoặc mỗi em một bản riêng.
    /// </summary>
    /// <remarks>
    /// Đường dùng chung của lúc giao bài và lúc sửa đề sinh lại bản đề — hai nơi phải ra cùng một kết
    /// quả cho cùng một cấu hình. Ghi bản đề chung xuống ngay (nhưng không commit) để lượt sau tìm thấy
    /// nó; người gọi phải đang mở transaction.
    /// </remarks>
    Task<Result> GeneratePapersAsync(
        Exam exam,
        IReadOnlyList<ExamAssignment> assignments,
        CancellationToken cancellationToken = default);
}
