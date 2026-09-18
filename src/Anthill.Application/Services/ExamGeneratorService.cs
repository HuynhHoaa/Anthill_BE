using Anthill.Application.Common;
using Anthill.Application.Interfaces.Repositories;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

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
    /// <summary>H10 — xem <see cref="CheckSharedPaperReleaseAsync"/>.</summary>
    private const string SharedPaperAlreadyReleasedMessage =
        "Đề này dùng chung một bản đề cho cả lớp, và đáp án của bản đề đó đã tới tay những em được giao "
        + "trước đây (cửa sổ làm bài của các em đó đã đóng, hoặc lượt làm dở muộn nhất đã hết hạn). Giao "
        + "thêm học sinh mới vào lúc này sẽ phát cho các em đúng bộ đề mà đáp án đã lộ. Hãy nhân bản đề "
        + "thành một đề mới rồi giao bản sao, hoặc bật \"mỗi học sinh một đề riêng\" — chỉ áp dụng được "
        + "cho đề chưa giao ai.";

    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Tập ứng viên đã đọc, dùng lại trong cùng một request.
    /// </summary>
    /// <remarks>
    /// Giao bài cho một lớp 40 em khi bật <c>randomizePerStudent</c> gọi hàm sinh đề 40 lần, mỗi
    /// lần ba mức độ — 120 lượt đọc trọn ngân hàng câu hỏi <b>kèm toàn bộ đáp án</b>, bên trong một
    /// transaction đang mở. Cùng một đề thì cả 40 lần đều hỏi đúng một câu hỏi, nên chỉ cần đọc
    /// một lần.
    ///
    /// Service đăng ký Scoped nên bộ đệm này sống đúng một request rồi biến mất — không có nguy cơ
    /// phục vụ dữ liệu cũ cho request sau, mà đó mới là điều đáng sợ ở một ngân hàng câu hỏi đang
    /// được đồng nghiệp sửa song song.
    /// </remarks>
    private readonly Dictionary<(int SubjectId, string? Topic, QuestionDifficulty Difficulty), IReadOnlyList<Question>>
        _candidateCache = [];

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

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Sinh <b>ngay lúc giao</b> chứ không đợi tới lúc học sinh mở bài. Sinh muộn thì một đề thiếu
    /// câu hỏi sẽ nổ ra trước mặt học sinh, giữa giờ kiểm tra, và người duy nhất sửa được lại
    /// không có mặt ở đó. Sinh sớm khiến thao tác giao bài chậm hơn một chút, đổi lại lỗi rơi
    /// đúng vào lúc giáo viên đang ngồi trước màn hình và còn sửa được. Sửa đề trước khi có em
    /// vào thi cũng đi qua đây, vì cùng lý do đó.
    /// </para>
    /// <para>
    /// Khi tắt <c>randomizePerStudent</c>, lượt đầu tiên tạo bản đề chung rồi những lượt sau nhận
    /// lại chính nó — cả lớp một bộ câu, đúng như cờ đó hứa hẹn.
    /// </para>
    /// </remarks>
    public async Task<Result> GeneratePapersAsync(
        Exam exam,
        IReadOnlyList<ExamAssignment> assignments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exam);
        ArgumentNullException.ThrowIfNull(assignments);

        if (!exam.RandomizePerStudent && assignments.Count > 0)
        {
            var releaseCheck = await CheckSharedPaperReleaseAsync(exam, assignments, cancellationToken);
            if (releaseCheck.IsFailure)
            {
                return releaseCheck;
            }
        }

        foreach (var assignment in assignments)
        {
            var paper = await GetOrCreatePaperAsync(exam, assignment, cancellationToken);
            if (paper.IsFailure)
            {
                return Result.Failure(paper.Error!, paper.ErrorCode);
            }

            // Bản đề chung dùng lại cho mọi em, nên chỉ cần sinh một lần. Ghi xuống ngay để lượt
            // sau tìm thấy nó thay vì sinh thêm một bản nữa.
            if (!exam.RandomizePerStudent)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                break;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// H10 — chặn giao thêm người mới vào một đề dùng bản đề chung mà đáp án đã lộ. Xem
    /// <see cref="PaperReleasePolicy"/> cho lý luận đầy đủ.
    /// </summary>
    /// <remarks>
    /// Chỉ có ý nghĩa ở đường giao bài. <c>ExamService.RegeneratePapersAsync</c> (H9) luôn xoá bản đề
    /// chung trước khi gọi <see cref="GeneratePapersAsync"/>, nên ở đường sửa đề <c>GetSharedPaperAsync</c>
    /// dưới đây luôn trả về NULL và hàm này luôn cho qua — sửa đề không bao giờ bị luật này chặn.
    /// </remarks>
    private async Task<Result> CheckSharedPaperReleaseAsync(
        Exam exam,
        IReadOnlyList<ExamAssignment> assignments,
        CancellationToken cancellationToken)
    {
        var existing = await _unitOfWork.Exams.GetSharedPaperAsync(exam.Id, cancellationToken);

        DateTime? releaseAt;
        bool hasAnyStartedAttempt;

        if (exam.AnswersReleasedAt is { } stamped)
        {
            // H10 (nửa release-flicker, xem AnswersReleaseStamp): đã có ResultService chốt mốc này từ
            // trước — dùng thẳng, khỏi tính động. Một mốc đã chốt chỉ có thể sinh ra từ việc đọc chi
            // tiết một attemptId CÓ THẬT, nên suy ra ngay hasAnyStartedAttempt = true.
            releaseAt = stamped;
            hasAnyStartedAttempt = true;
        }
        else
        {
            // Loại các lượt giao ĐANG được xét ra khỏi phép tính mốc mở đáp án: chúng vừa được ghi
            // xuống để lấy id thật, nhưng giờ đóng của chúng thường muộn hơn các lượt cũ — tính luôn vào
            // MAX sẽ đẩy mốc "đã mở đáp án" ra tương lai và che mất việc lớp cũ đã thấy đáp án.
            var excludeIds = assignments.Select(assignment => assignment.Id).ToList();
            releaseAt = await _unitOfWork.ExamAssignments.GetAnswersReleaseAtAsync(
                exam.Id, excludeIds, cancellationToken);

            // Lượt giao mới vừa được ghi xuống nên không thể có lượt làm bài nào — đếm luôn trên toàn đề
            // là đúng số của riêng các lượt giao CŨ, không cần loại trừ thêm.
            var startedCount = await _unitOfWork.ExamAssignments.CountStartedByExamAsync(exam.Id, cancellationToken);
            hasAnyStartedAttempt = startedCount > 0;
        }

        var decision = PaperReleasePolicy.DecideSharedPaperReuse(
            exam.RandomizePerStudent,
            sharedPaperExists: existing is not null,
            hasAnyStartedAttempt,
            priorReleaseAt: releaseAt,
            asOfUtc: DateTime.UtcNow);

        return decision == SharedPaperAssignmentDecision.RejectAlreadyReleased
            ? Result.Failure(SharedPaperAlreadyReleasedMessage, ErrorCode.Conflict)
            : Result.Success();
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

            var cacheKey = (subjectId, topic, difficulty);

            if (!_candidateCache.TryGetValue(cacheKey, out var candidates))
            {
                candidates = await _unitOfWork.Questions.GetBlueprintCandidatesAsync(
                    subjectId,
                    ownerUserId,
                    topic,
                    difficulty,
                    cancellationToken);

                _candidateCache[cacheKey] = candidates;
            }

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
