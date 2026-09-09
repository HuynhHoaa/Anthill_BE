using EduQuiz.Application.Common;
using EduQuiz.Application.Interfaces.Repositories;
using EduQuiz.Application.Interfaces.Services;
using EduQuiz.Application.Interfaces.UnitOfWork;
using EduQuiz.Application.Mappings;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Services;

/// <summary>
/// Cài đặt <see cref="IExamGeneratorService"/> — dựng bản đề thật từ blueprint.
/// </summary>
/// <remarks>
/// Ba điều quyết định nằm ở file này:
/// <list type="number">
/// <item><b>Bản đề là một bản chụp, không phải một con trỏ.</b> Nội dung câu hỏi, ảnh, điểm và
/// từng đáp án đều được sao vào <c>ExamPaperQuestion</c>/<c>ExamPaperAnswer</c> tại thời điểm
/// sinh. Mock hiện tra ngược bảng câu hỏi theo id lúc chấm, nghĩa là giáo viên sửa một câu hỏi
/// hôm nay làm đổi điểm những bài đã chấm tuần trước. Chụp lại là cách duy nhất để một bài đã
/// chấm giữ nguyên ý nghĩa.</item>
/// <item><b>Hạt giống quyết định bản đề, và nó gắn với assignment chứ không với lượt làm bài.</b>
/// Học sinh mất mạng rồi vào lại phải thấy đúng bộ câu cũ.</item>
/// <item><b>Bốc câu bằng Fisher-Yates có seed, không dùng <c>ORDER BY RANDOM()</c>.</b> Ngoài
/// chuyện tái lập, còn một lý do thực dụng: <c>ORDER BY RANDOM()</c> bắt PostgreSQL sinh khoá
/// ngẫu nhiên rồi sắp xếp toàn bộ bảng câu hỏi cho mỗi lần sinh đề, mà với 40 học sinh mỗi em một
/// đề thì đó là 40 lần quét.</item>
/// </list>
/// </remarks>
public sealed class ExamGeneratorService : IExamGeneratorService
{
    private readonly IUnitOfWork _unitOfWork;

    public ExamGeneratorService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result<ExamPaper>> GetOrCreatePaperAsync(
        Exam exam,
        ExamAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exam);
        ArgumentNullException.ThrowIfNull(assignment);

        var existing = await FindExistingPaperAsync(exam, assignment, cancellationToken);
        if (existing is not null)
        {
            return Result.Success(existing);
        }

        // Môn của đề suy từ người tạo — Exam cố ý không có cột SubjectId. Đọc từ đây thay vì từ
        // claim của người đang gọi, vì bước 6 sẽ gọi hàm này trong ngữ cảnh học sinh, mà học sinh
        // thì không mang claim subject_id nào.
        var owner = await _unitOfWork.Users.GetByIdAsync(exam.CreatedByUserId, cancellationToken);
        if (owner?.SubjectId is null)
        {
            return Result.Failure<ExamPaper>(
                "Giáo viên tạo đề này chưa được gán môn, nên không xác định được ngân hàng câu hỏi để rút.",
                ErrorCode.Conflict);
        }

        var seed = ResolveSeed(exam, assignment);

        var picked = await PickQuestionsAsync(exam, owner.SubjectId.Value, owner.Id, seed, cancellationToken);
        if (picked.IsFailure)
        {
            return Result.Failure<ExamPaper>(picked.Error!, picked.ErrorCode);
        }

        var paper = BuildPaper(exam, assignment, picked.ValueOrThrow, seed);

        await _unitOfWork.Exams.AddPaperAsync(paper, cancellationToken);

        // Cố ý không SaveChanges ở đây: người gọi đang mở transaction bao cả việc tạo assignment.
        return Result.Success(paper);
    }

    /// <summary>
    /// Bản đề đã có sẵn, nếu có: bản riêng của assignment khi bật cờ, bản chung của đề khi tắt.
    /// </summary>
    private async Task<ExamPaper?> FindExistingPaperAsync(
        Exam exam,
        ExamAssignment assignment,
        CancellationToken cancellationToken) =>
        exam.RandomizePerStudent
            ? await _unitOfWork.Exams.GetPaperByAssignmentAsync(assignment.Id, cancellationToken)
            : await _unitOfWork.Exams.GetSharedPaperAsync(exam.Id, cancellationToken);

    /// <summary>
    /// Hạt giống của bản đề.
    /// </summary>
    /// <remarks>
    /// Tắt cờ thì seed chỉ theo <c>examId</c>, nên mọi học sinh nhận đúng một bộ câu — điều kiện
    /// để so sánh kết quả giữa các em với nhau. Bật thì thêm <c>assignmentId</c> vào, nên mỗi em
    /// một bộ, mà vẫn cố định qua các lần vào lại.
    /// </remarks>
    private static uint ResolveSeed(Exam exam, ExamAssignment assignment) =>
        DeterministicShuffle.Seed(
            exam.RandomizePerStudent
                ? $"{exam.Id}:{assignment.Id}"
                : exam.Id.ToString());

    /// <summary>
    /// Bốc đủ số câu từng mức độ theo blueprint.
    /// </summary>
    /// <remarks>
    /// Đọc trọn tập ứng viên của từng mức rồi xáo trong bộ nhớ. Với một ngân hàng cỡ vài nghìn câu
    /// mỗi môn thì rẻ hơn hẳn việc bắt database sắp xếp ngẫu nhiên, và đổi lại được tính tái lập.
    /// </remarks>
    private async Task<Result<List<Question>>> PickQuestionsAsync(
        Exam exam,
        int subjectId,
        int ownerUserId,
        uint baseSeed,
        CancellationToken cancellationToken)
    {
        var required = BlueprintCalculator.CalculateRequiredCounts(
            exam.TotalQuestions,
            exam.EasyPercentage,
            exam.MediumPercentage);

        var topic = ExamMappingExtensions.NormalizeTopic(exam.Topic);
        var picked = new List<Question>(exam.TotalQuestions);

        foreach (var difficulty in Difficulties)
        {
            var need = required[difficulty];
            if (need == 0)
            {
                continue;
            }

            var candidates = await _unitOfWork.Questions.GetBlueprintCandidatesAsync(
                subjectId,
                ownerUserId,
                topic,
                difficulty,
                cancellationToken);

            if (candidates.Count < need)
            {
                // Blueprint từng hợp lệ lúc lưu đề vẫn có thể hỏng ở đây: đồng nghiệp cùng tổ ẩn
                // bớt câu trong khoảng thời gian giữa hai thời điểm đó.
                return Result.Failure<List<Question>>(
                    $"Không đủ câu {difficulty.ToDifficultyLabel()} để sinh đề: cần {need} "
                    + $"nhưng ngân hàng chỉ còn {candidates.Count}. "
                    + "Hãy bổ sung câu hỏi hoặc chỉnh lại tỷ lệ của đề.",
                    ErrorCode.Conflict);
            }

            // Xáo cả tập rồi lấy phần đầu: mỗi tổ hợp câu có cơ hội như nhau, khác với việc bốc
            // dần từng câu theo chỉ số ngẫu nhiên rồi phải xử lý trùng.
            // Mỗi mức độ một hạt giống dẫn xuất từ hạt giống gốc. Dùng chung một hạt cho cả ba
            // mức thì ba lần xáo trên ba tập khác nhau vẫn độc lập, nhưng tách ra thế này khiến
            // việc lần lại một bản đề cụ thể dễ hơn khi cần đối chiếu.
            var levelSeed = DeterministicShuffle.Seed($"{baseSeed}:{difficulty}");
            picked.AddRange(DeterministicShuffle.Shuffle(candidates, levelSeed).Take(need));
        }

        return Result.Success(picked);
    }

    /// <summary>
    /// Dựng bản đề: chụp lại nội dung, xếp thứ tự câu và đáp án, gán <c>Ordinal</c>.
    /// </summary>
    /// <remarks>
    /// Thứ tự được <b>chốt tại đây và lưu xuống database</b>. Tầng trả response chỉ việc đọc theo
    /// <c>Ordinal</c> — xáo lại ở đó thì mỗi lần học sinh tải lại trang sẽ thấy một thứ tự khác,
    /// và mọi câu trả lời đã lưu theo vị trí đều lệch.
    /// </remarks>
    private static ExamPaper BuildPaper(
        Exam exam,
        ExamAssignment assignment,
        List<Question> questions,
        uint seed)
    {
        var ordered = exam.ShuffleQuestions
            ? DeterministicShuffle.Shuffle(questions, DeterministicShuffle.Seed($"{seed}:questions"))
            : questions;

        var paper = new ExamPaper
        {
            ExamId = exam.Id,

            // Bản chung của cả lớp không gắn với assignment nào, nên cột này để NULL. Đó cũng là
            // dấu hiệu để lần đọc sau nhận ra nên tìm bản chung hay bản riêng.
            AssignmentId = exam.RandomizePerStudent ? assignment.Id : null,

            CreatedAt = DateTime.UtcNow
        };

        for (var index = 0; index < ordered.Count; index++)
        {
            var question = ordered[index];

            var answers = exam.ShuffleAnswers
                ? DeterministicShuffle.Shuffle(
                    question.Answers.OrderBy(answer => answer.Ordinal).ToList(),
                    DeterministicShuffle.Seed($"{seed}:answers:{question.Id}"))
                : question.Answers.OrderBy(answer => answer.Ordinal).ToList();

            var paperQuestion = new ExamPaperQuestion
            {
                QuestionId = question.Id,
                Ordinal = index,

                // Bốn dòng chụp lại dưới đây là lý do tồn tại của cả bảng này.
                ContentSnapshot = question.Content,
                ImageUrlSnapshot = question.ImageUrl,

                // Điểm lấy từ đề, không lấy từ DefaultPoint của câu hỏi: cùng một câu có thể đáng
                // 0,5 điểm trong bài 15 phút và 1 điểm trong bài kiểm tra một tiết.
                Point = exam.PointPerQuestion,

                CreatedAt = paper.CreatedAt
            };

            for (var answerIndex = 0; answerIndex < answers.Count; answerIndex++)
            {
                var answer = answers[answerIndex];

                paperQuestion.Answers.Add(new ExamPaperAnswer
                {
                    ContentSnapshot = answer.Content,
                    ImageUrlSnapshot = answer.ImageUrl,
                    IsCorrect = answer.IsCorrect,
                    Ordinal = answerIndex,
                    CreatedAt = paper.CreatedAt
                });
            }

            paper.Questions.Add(paperQuestion);
        }

        return paper;
    }

    private static readonly QuestionDifficulty[] Difficulties =
    [
        QuestionDifficulty.Easy,
        QuestionDifficulty.Medium,
        QuestionDifficulty.Hard
    ];
}
