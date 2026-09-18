using Anthill.Application.Common;
using Anthill.Application.DTOs.Questions;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

/// <summary>
/// Cài đặt <see cref="IQuestionService"/> — tám endpoint của §4.5.
/// </summary>
/// <remarks>
/// Bốn điều quyết định nằm ở file này:
/// <list type="number">
/// <item><b>Hai cột phân định làm hai việc khác nhau</b> (§6). <c>SubjectId</c> trả lời "ai được
/// thấy" và áp ở tầng repository cho mọi truy vấn đọc; <c>CreatedByUserId</c> trả lời "ai được
/// sửa" và kiểm ở đây. Trộn hai thứ lại thì hoặc giáo viên không thấy được câu của đồng nghiệp
/// cùng môn, hoặc sửa được câu của họ — cả hai đều sai.</item>
/// <item><b>Sửa câu hỏi là thay cả mảng đáp án</b> (§4.5), không sửa tại chỗ. Bài đã chấm không
/// việc gì vì chúng chấm bằng bản chụp trong <c>ExamPaperAnswer</c>.</item>
/// <item><b>Xoá phải kiểm câu đã nằm trong đề chưa.</b> Frontend xoá thẳng; ở đây từ chối bằng
/// <c>Conflict</c>, vì xoá lúc đó là xoá luôn dữ liệu của những bài đã làm.</item>
/// <item><b>Ngoài phạm vi thì trả <c>NotFound</c>, không phải <c>Forbidden</c></b> — trả 403 là
/// xác nhận id đó có thật (§4.2).</item>
/// </list>
/// </remarks>
public sealed class QuestionService : IQuestionService
{
    private const string NotFoundMessage = "Không tìm thấy câu hỏi.";
    private const string NoSubjectMessage =
        "Tài khoản giáo viên chưa được gán môn, nên chưa có ngân hàng câu hỏi nào để mở. "
        + "Liên hệ quản trị viên để gán môn.";
    private const string AdminNeedsSubjectMessage =
        "Quản trị viên phải chọn môn cho câu hỏi bằng trường 'subjectId'.";
    private const string CannotEditMessage =
        "Câu hỏi này do giáo viên khác soạn nên bạn chỉ xem được, không sửa được.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IExamGeneratorService _examGenerator;

    public QuestionService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IExamGeneratorService examGenerator)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _examGenerator = examGenerator;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<QuestionDto>>> ListAsync(
        ListQuestionsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = ResolveReadScope(query.SubjectId);
        if (scope.IsFailure)
        {
            return Result.Failure<PagedResult<QuestionDto>>(scope.Error!, scope.ErrorCode);
        }

        if (!QuestionMappingExtensions.TryParseDifficultyFilter(query.Difficulty, out var difficulty))
        {
            return Result.Failure<PagedResult<QuestionDto>>(
                "Bộ lọc mức độ chỉ nhận 'easy', 'medium', 'hard' hoặc 'all'.");
        }

        if (!QuestionMappingExtensions.TryParseQuestionStatusFilter(query.Status, out var status))
        {
            return Result.Failure<PagedResult<QuestionDto>>(
                "Bộ lọc trạng thái chỉ nhận 'active', 'hidden' hoặc 'all'.");
        }

        var page = await _unitOfWork.Questions.GetPagedAsync(
            scope.ValueOrThrow,
            CurrentUserId,
            query.Search,
            QuestionMappingExtensions.NormalizeTopicFilter(query.Topic),
            difficulty,
            status,
            query,
            cancellationToken);

        return Result.Success(page.Map(question => question.ToDto(CanEdit(question))));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> ListTopicsAsync(
        int? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = ResolveReadScope(subjectId);
        if (scope.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(scope.Error!, scope.ErrorCode);
        }

        var topics = await _unitOfWork.Questions.GetTopicsAsync(
            scope.ValueOrThrow,
            CurrentUserId,
            cancellationToken);

        return Result.Success(topics);
    }

    /// <inheritdoc />
    public async Task<Result<QuestionBankSummaryDto>> GetSummaryAsync(
        int? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = ResolveReadScope(subjectId);
        if (scope.IsFailure)
        {
            return Result.Failure<QuestionBankSummaryDto>(scope.Error!, scope.ErrorCode);
        }

        var scopeSubjectId = scope.ValueOrThrow;

        // Ba con số theo mức độ tính trên TOÀN BỘ ngân hàng kể cả câu đang ẩn, giống hệt
        // countByDifficulty() của frontend. Số câu dùng được để sinh đề là chuyện của bước
        // xem trước blueprint, nơi chỉ đếm câu Active.
        var byDifficulty = await _unitOfWork.Questions.CountByDifficultyAsync(
            scopeSubjectId, CurrentUserId, topic: null, status: null, cancellationToken);

        var activeByDifficulty = await _unitOfWork.Questions.CountByDifficultyAsync(
            scopeSubjectId, CurrentUserId, topic: null, QuestionStatus.Active, cancellationToken);

        var topics = await _unitOfWork.Questions.GetTopicsAsync(scopeSubjectId, CurrentUserId, cancellationToken);

        var total = byDifficulty.Values.Sum();
        var active = activeByDifficulty.Values.Sum();

        return Result.Success(new QuestionBankSummaryDto
        {
            Total = total,
            Active = active,

            // Suy ra thay vì đếm lần thứ ba: hai trạng thái là toàn bộ tập giá trị của
            // QuestionStatus, nên phần còn lại chắc chắn là Hidden.
            Hidden = total - active,

            Easy = byDifficulty[QuestionDifficulty.Easy],
            Medium = byDifficulty[QuestionDifficulty.Medium],
            Hard = byDifficulty[QuestionDifficulty.Hard],
            Topics = topics
        });
    }

    /// <inheritdoc />
    public async Task<Result<QuestionDto>> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var question = await _unitOfWork.Questions.GetWithAnswersAsync(id, cancellationToken);
        if (question is null || !IsReadable(question))
        {
            return Result.NotFound<QuestionDto>(NotFoundMessage);
        }

        return Result.Success(question.ToDto(CanEdit(question)));
    }

    /// <inheritdoc />
    public async Task<Result<QuestionDto>> CreateAsync(
        SaveQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetCurrentUserId(out var userId))
        {
            return Result.Unauthorized<QuestionDto>("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }

        var subject = await ResolveWriteSubjectAsync(request.SubjectId, cancellationToken);
        if (subject.IsFailure)
        {
            return Result.Failure<QuestionDto>(subject.Error!, subject.ErrorCode);
        }

        var fields = QuestionFields.From(request);

        var question = new Question
        {
            Content = fields.Content,
            ImageUrl = fields.ImageUrl,
            Topic = fields.Topic,
            Difficulty = fields.Difficulty,
            DefaultPoint = request.DefaultPoint,
            Status = fields.Status ?? QuestionStatus.Active,
            Visibility = fields.Visibility ?? QuestionVisibility.Shared,
            SubjectId = subject.ValueOrThrow,

            // QUY TRÁCH: người soạn là người đang đăng nhập, không nhận từ body (§6).
            CreatedByUserId = userId
        };

        BuildAnswers(question, request.Answers);

        await _unitOfWork.Questions.AddAsync(question, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Nạp lại để lấy navigation CreatedBy cho cột "Soạn bởi"; entity vừa thêm chỉ có khoá ngoại.
        return await ReloadDtoAsync(question.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<QuestionDto>> UpdateAsync(
        int id,
        SaveQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return Result.Failure<QuestionDto>(load.Error!, load.ErrorCode);
        }

        var question = load.ValueOrThrow;
        var fields = QuestionFields.From(request);

        question.Content = fields.Content;
        question.ImageUrl = fields.ImageUrl;
        question.Topic = fields.Topic;
        question.Difficulty = fields.Difficulty;
        question.DefaultPoint = request.DefaultPoint;
        question.Status = fields.Status ?? question.Status;
        question.Visibility = fields.Visibility ?? question.Visibility;
        question.UpdatedAt = DateTime.UtcNow;

        // SubjectId và CreatedByUserId cố ý KHÔNG đổi: câu hỏi không chuyển môn giữa chừng, và
        // người sửa không cướp quyền tác giả của người soạn.

        // §4.5: thay CẢ mảng đáp án. Xoá sạch rồi thêm mới thay vì so từng phần tử — quan hệ khai
        // Cascade nên EF phát lệnh DELETE cho đáp án cũ, và id mới được cấp lại từ đầu. Nghe có vẻ
        // phí, nhưng chính nó là thứ bảo đảm không tồn tại đường nào sửa lẻ một đáp án của câu hỏi
        // đang nằm trong đề. Bài đã chấm không việc gì: chúng chấm bằng ExamPaperAnswer.
        question.Answers.Clear();
        BuildAnswers(question, request.Answers);

        _unitOfWork.Questions.Update(question);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // H9: nội dung/đáp án vừa đổi (kể cả đáp án đúng) — mọi đề mà bản đề ĐÃ SINH đang chứa câu này
        // và chưa ai bắt đầu làm phải được sinh lại, để không còn ai bị chấm theo bản chụp cũ.
        await RegenerateAffectedExamsAsync(question.Id, cancellationToken);

        return await ReloadDtoAsync(question.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return load;
        }

        var question = load.ValueOrThrow;

        // Frontend xoá thẳng, không kiểm gì (§4.5). Ở đây bắt buộc phải kiểm: một câu đã vào đề
        // nghĩa là có học sinh đã nhìn thấy nó, có thể đã trả lời nó. Khoá ngoại từ
        // ExamPaperQuestion sẽ chặn ở database, nhưng chặn ở đó thì lỗi ra ngoài dưới dạng 500.
        if (await _unitOfWork.Questions.IsUsedInAnyPaperAsync(question.Id, cancellationToken))
        {
            return Result.Conflict(
                "Câu hỏi đã được đưa vào một bài kiểm tra nên không xoá được. "
                + "Hãy ẩn câu hỏi để nó không xuất hiện trong các đề sinh sau này.");
        }

        _unitOfWork.Questions.Remove(question);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<QuestionDto>> SetStatusAsync(
        int id,
        QuestionStatus status,
        CancellationToken cancellationToken = default)
    {
        var load = await LoadWritableAsync(id, cancellationToken);
        if (load.IsFailure)
        {
            return Result.Failure<QuestionDto>(load.Error!, load.ErrorCode);
        }

        var question = load.ValueOrThrow;

        if (question.Status != status)
        {
            // Ẩn loại câu khỏi lần sinh đề SAU (bộ sinh đề chỉ đọc câu Active). Đề đã sinh cho lượt
            // CHƯA bắt đầu thì không "giữ nguyên vì dùng bản chụp" theo nghĩa tốt — H9: bản chụp đó có
            // thể vẫn đang chứa chính câu vừa bị ẩn, và học sinh chưa vào thi thì vẫn còn kịp sinh lại
            // cho đúng ý giáo viên. Học sinh ĐANG làm bài dở thì không đụng: xem RegenerateAffectedExamsAsync.
            question.Status = status;
            question.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Questions.Update(question);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (status == QuestionStatus.Hidden)
            {
                await RegenerateAffectedExamsAsync(question.Id, cancellationToken);
            }
        }

        return await ReloadDtoAsync(question.Id, cancellationToken);
    }

    /// <summary>
    /// H9: sau khi nội dung câu hỏi vừa đổi (sửa hoặc ẩn), sinh lại bản đề của mọi đề mà bản đề ĐÃ SINH
    /// đang chứa câu này, cho những đề chưa ai bắt đầu làm — mirror đúng ranh giới "lượt làm bài đầu
    /// tiên" mà <c>ExamEditPolicy</c> dùng khi sửa cả đề (xem <see cref="ExamEditPolicy.DecidePaperRefresh"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cố ý <b>best-effort</b>, không bao giờ làm hỏng lần sửa câu hỏi: nội dung câu hỏi (đã lưu ở lệnh
    /// gọi trước hàm này) là nguồn sự thật, còn việc sinh lại bản đề của một đề khác là một hệ quả phụ.
    /// Ngân hàng không đủ câu cho cấu hình của MỘT đề nào đó sau khi câu này đổi — hoàn toàn có thể xảy
    /// ra và không liên quan gì tới ý định của giáo viên lúc sửa câu hỏi — chỉ khiến đề đó giữ nguyên
    /// bản đề cũ (đúng hiện trạng trước khi sửa, không tệ hơn), không chặn hay cuộn lại việc sửa câu hỏi.
    /// </para>
    /// <para>
    /// Đề đang có em bắt đầu làm bị bỏ qua hoàn toàn, kể cả không xoá bản đề — đúng ranh giới "khoá sau
    /// lượt làm bài đầu tiên", và cũng là hành vi mà <c>exam-edit-rules.sh</c> ("ẩn một câu Dễ") đã chốt:
    /// ẩn một câu đang nằm trong bản đề của đề đã khoá vẫn phải trả 200 mà không đụng gì tới bản đề đó.
    /// </para>
    /// </remarks>
    private async Task RegenerateAffectedExamsAsync(int questionId, CancellationToken cancellationToken)
    {
        var examIds = await _unitOfWork.Exams.GetExamIdsUsingQuestionAsync(questionId, cancellationToken);

        foreach (var examId in examIds)
        {
            await RegenerateOneExamPapersAsync(examId, cancellationToken);
        }
    }

    /// <summary>Sinh lại (hoặc bỏ) bản đề của một đề duy nhất, dưới đúng khoá mà <c>ExamService.UpdateAsync</c> dùng.</summary>
    private async Task RegenerateOneExamPapersAsync(int examId, CancellationToken cancellationToken)
    {
        // Cùng thứ tự khoá với ExamService.UpdateAsync: dòng đề trước, dòng giao bài sau (§5) — để lần
        // sinh lại này xếp hàng đúng chỗ với một lần sửa đề hay một em đang bấm "Vào thi" trên đề đó.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var exam = await _unitOfWork.Exams.LockForEditAsync(examId, cancellationToken);
        if (exam is null)
        {
            // Đề vừa bị xoá giữa chừng — không còn gì để sinh lại. Không commit gì thì Dispose tự rollback.
            return;
        }

        var assignments = await _unitOfWork.ExamAssignments.LockByExamAsync(examId, cancellationToken);
        var startedCount = assignments.Count == 0
            ? 0
            : await _unitOfWork.ExamAssignments.CountStartedByExamAsync(examId, cancellationToken);

        var decision = ExamEditPolicy.DecidePaperRefresh(
            hasAssignments: assignments.Count > 0,
            hasStartedAttempts: startedCount > 0);

        if (decision == ExamEditPolicy.PaperRefreshDecision.Skip)
        {
            return;
        }

        await _unitOfWork.Exams.RemovePapersAsync(examId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (decision == ExamEditPolicy.PaperRefreshDecision.Regenerate)
        {
            var regenerated = await _examGenerator.GeneratePapersAsync(exam, assignments, cancellationToken);
            if (regenerated.IsFailure)
            {
                // Ngân hàng không còn đủ câu cho cấu hình hiện tại của đề NÀY, riêng sau câu hỏi vừa đổi.
                // Cuộn lại đúng lệnh xoá bản đề của đề này — không kéo theo lần sửa câu hỏi (đã lưu và
                // commit trước khi hàm này được gọi) hay các đề khác trong cùng lô. Bản đề cũ của đề này
                // giữ nguyên, đúng hiện trạng trước khi sửa câu hỏi.
                await transaction.RollbackAsync(cancellationToken);
                return;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Phạm vi <b>đọc</b>: môn nào được nhìn thấy.
    /// </summary>
    /// <remarks>
    /// Giáo viên bị khoá vào môn của mình và tham số <c>subjectId</c> trên URL bị bỏ qua — cho phép
    /// nó có tác dụng nghĩa là đọc được ngân hàng của tổ bộ môn khác chỉ bằng cách đổi một con số
    /// trên thanh địa chỉ. Quản trị viên không thuộc môn nào nên mặc định đọc tất cả, và dùng tham
    /// số đó để thu hẹp.
    /// </remarks>
    private Result<int?> ResolveReadScope(int? requestedSubjectId)
    {
        if (_currentUser.IsInRole(UserRole.Admin))
        {
            return Result.Success(requestedSubjectId);
        }

        return _currentUser.SubjectId is { } subjectId
            ? Result.Success<int?>(subjectId)
            : Result.Forbidden<int?>(NoSubjectMessage);
    }

    /// <summary>
    /// Môn của câu hỏi <b>sắp tạo</b>. Giáo viên lấy từ claim; quản trị viên phải nói rõ.
    /// </summary>
    private async Task<Result<int>> ResolveWriteSubjectAsync(
        int? requestedSubjectId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
        {
            return _currentUser.SubjectId is { } subjectId
                ? Result.Success(subjectId)
                : Result.Forbidden<int>(NoSubjectMessage);
        }

        if (requestedSubjectId is not { } adminSubjectId)
        {
            return Result.Failure<int>(AdminNeedsSubjectMessage);
        }

        // Môn không có thật đi thẳng xuống database sẽ vỡ ở khoá ngoại thành lỗi 500.
        var exists = await _unitOfWork.Subjects.GetByIdAsync(adminSubjectId, cancellationToken) is not null;

        return exists
            ? Result.Success(adminSubjectId)
            : Result.Failure<int>("Môn học không tồn tại.");
    }

    /// <summary>
    /// Nạp câu hỏi cho thao tác <b>ghi</b>: phải vừa nhìn thấy được, vừa có quyền sửa.
    /// </summary>
    /// <remarks>
    /// Hai bước tách bạch và trả hai mã khác nhau, vì chúng là hai tình huống khác nhau với người
    /// dùng: câu ngoài phạm vi thì <c>NotFound</c> (không được biết nó tồn tại), còn câu nhìn thấy
    /// được nhưng của đồng nghiệp thì <c>Forbidden</c> kèm câu giải thích — giấu đi ở đây chỉ khiến
    /// giáo viên tưởng ngân hàng bị lỗi, trong khi họ đang <i>đọc</i> chính câu đó trên màn hình.
    /// </remarks>
    private async Task<Result<Question>> LoadWritableAsync(int id, CancellationToken cancellationToken)
    {
        var question = await _unitOfWork.Questions.GetWithAnswersAsync(id, cancellationToken);
        if (question is null || !IsReadable(question))
        {
            return Result.NotFound<Question>(NotFoundMessage);
        }

        return CanEdit(question)
            ? Result.Success(question)
            : Result.Forbidden<Question>(CannotEditMessage);
    }

    /// <summary>
    /// Phạm vi đọc của §6, áp lại cho đường tra theo id.
    /// </summary>
    /// <remarks>
    /// <c>GetWithAnswersAsync</c> tra thẳng theo khoá chính nên <b>không</b> mang theo mệnh đề phạm
    /// vi như các truy vấn danh sách. Thiếu phép kiểm này thì gõ tay một id là đọc được câu hỏi của
    /// tổ bộ môn khác — đúng loại lỗ hổng mà việc tách <c>SubjectId</c> ra sinh ra để chặn.
    /// </remarks>
    private bool IsReadable(Question question)
    {
        if (_currentUser.IsInRole(UserRole.Admin))
        {
            return true;
        }

        if (_currentUser.SubjectId != question.SubjectId)
        {
            return false;
        }

        return question.Visibility == QuestionVisibility.Shared
               || question.CreatedByUserId == CurrentUserId;
    }

    /// <summary>Quyền ghi của §6: <c>CreatedByUserId == currentUserId || Role == Admin</c>.</summary>
    private bool CanEdit(Question question) =>
        _currentUser.IsInRole(UserRole.Admin) || question.CreatedByUserId == CurrentUserId;

    /// <summary>
    /// Dựng lại đáp án từ request. Thứ tự phần tử trong mảng chính là <c>Ordinal</c> — không nhận
    /// trường thứ tự riêng để khỏi phải xử lý trường hợp nó mâu thuẫn với thứ tự mảng.
    /// </summary>
    private static void BuildAnswers(Question question, IReadOnlyList<SaveAnswerOptionRequest> answers)
    {
        for (var index = 0; index < answers.Count; index++)
        {
            var answer = answers[index];

            question.Answers.Add(new AnswerOption
            {
                // Đáp án chỉ gồm một hình là hợp lệ, nên chuỗi rỗng ở đây có nghĩa chứ không phải thiếu.
                Content = answer.Content?.Trim() ?? string.Empty,
                ImageUrl = NormalizeOptional(answer.ImageUrl),
                IsCorrect = answer.IsCorrect,
                Ordinal = index
            });
        }
    }

    /// <summary>
    /// Nạp lại kèm navigation rồi dựng DTO. Sau <c>SaveChangesAsync</c>, entity đang bị theo dõi
    /// chỉ có <c>CreatedByUserId</c> chứ chưa có <c>CreatedBy</c>, mà cột "Soạn bởi" cần tên người.
    /// </summary>
    private async Task<Result<QuestionDto>> ReloadDtoAsync(int id, CancellationToken cancellationToken)
    {
        var saved = await _unitOfWork.Questions.GetWithAnswersAsync(id, cancellationToken);

        return saved is null
            ? Result.NotFound<QuestionDto>(NotFoundMessage)
            : Result.Success(saved.ToDto(CanEdit(saved)));
    }

    private int CurrentUserId => _currentUser.UserId ?? 0;

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = CurrentUserId;
        return _currentUser.IsAuthenticated && _currentUser.UserId.HasValue;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>
    /// Các trường đã trim và đã đọc sang enum, gói lại để không đường nào lỡ dùng chuỗi gốc.
    /// </summary>
    /// <remarks>
    /// <c>Status</c> và <c>Visibility</c> nullable vì bỏ trống mang hai nghĩa khác nhau tuỳ chỗ
    /// gọi: lúc tạo là "dùng mặc định", lúc sửa là "giữ nguyên giá trị đang có".
    /// </remarks>
    private readonly record struct QuestionFields(
        string Content,
        string? ImageUrl,
        string Topic,
        QuestionDifficulty Difficulty,
        QuestionStatus? Status,
        QuestionVisibility? Visibility)
    {
        public static QuestionFields From(SaveQuestionRequest request)
        {
            // Validator đã chặn giá trị lạ trước khi tới đây, nên TryParse thất bại chỉ xảy ra khi
            // service được gọi từ chỗ không đi qua pipeline. Rơi về mặc định an toàn thay vì ném.
            QuestionMappingExtensions.TryParseDifficulty(request.Difficulty, out var difficulty);

            return new QuestionFields(
                request.Content?.Trim() ?? string.Empty,
                NormalizeOptional(request.ImageUrl),
                request.Topic?.Trim() ?? string.Empty,
                difficulty,
                QuestionMappingExtensions.TryParseQuestionStatus(request.Status, out var status) ? status : null,
                QuestionMappingExtensions.TryParseVisibility(request.Visibility, out var visibility) ? visibility : null);
        }
    }
}
