using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.Exams;
using EduQuiz.Application.Interfaces.Security;
using EduQuiz.Application.Interfaces.Services;
using EduQuiz.Application.Interfaces.UnitOfWork;
using EduQuiz.Application.Mappings;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Services;

/// <summary>
/// Cài đặt <see cref="IExamService"/> — chín endpoint của §4.6.
/// </summary>
/// <remarks>
/// Bốn điều quyết định nằm ở file này:
/// <list type="number">
/// <item><b>Đề là cấu hình, không phải danh sách câu hỏi.</b> Sửa tỷ lệ độ khó của một đề chưa
/// giao không phải dọn dẹp gì cả, vì chưa có câu hỏi nào được chốt. Câu hỏi chỉ xuất hiện khi
/// sinh <c>ExamPaper</c> ở bước giao bài.</item>
/// <item><b>Nháp là một trạng thái được miễn trừ, không phải một cái nhãn.</b> Bản nháp bỏ qua hai
/// điều kiện nặng nhất — tổng ba tỷ lệ bằng 100 và ngân hàng đủ câu — vì mục đích của nháp là lưu
/// một cấu hình <i>chưa</i> hoàn chỉnh. Đổi lại, mọi đường ra khỏi trạng thái nháp
/// (<see cref="PublishAsync"/>, hoặc <see cref="UpdateAsync"/> với <c>lifecycle</c> bỏ trống) đều
/// phải qua đủ hai điều kiện đó.</item>
/// <item><b>Vòng đời tính lúc đọc, không lưu cứng</b> (§5). Mỗi thao tác đọc giờ server đúng một
/// lần rồi truyền xuống, để hai bản ghi trong cùng một response không bị đánh giá theo hai thời
/// điểm khác nhau.</item>
/// <item><b>Đề của giáo viên khác trả <c>NotFound</c>, không phải <c>Forbidden</c></b> — trả 403
/// là xác nhận id đó có thật, đủ để dò dần danh sách đề của đồng nghiệp (§4.2).</item>
/// </list>
/// </remarks>
public sealed class ExamService : IExamService
{
    private const string NotFoundMessage = "Không tìm thấy bài kiểm tra.";

    private const string NoSubjectMessage =
        "Tài khoản giáo viên chưa được gán môn, nên chưa có ngân hàng câu hỏi nào để ra đề. "
        + "Liên hệ quản trị viên để gán môn.";

    private const string AdminCannotWriteMessage =
        "Quản trị viên chỉ xem được bài kiểm tra; việc ra đề thuộc về giáo viên phụ trách môn.";

    private const string HasSubmissionsMessage =
        "Không thể xoá bài kiểm tra đã có học sinh nộp bài. Hãy để đề tự đóng theo lịch thay vì xoá, "
        + "để kết quả của các em vẫn tra cứu được.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public ExamService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    private int CurrentUserId => _currentUser.RequireUserId();

    private bool IsAdmin => _currentUser.IsInRole(UserRole.Admin);

    /// <inheritdoc />
    public async Task<Result<PagedResult<ExamDto>>> ListAsync(
        ListExamsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!ExamMappingExtensions.TryParseLifecycleFilter(query.Lifecycle, out var lifecycle))
        {
            return Result.Failure<PagedResult<ExamDto>>(
                "Bộ lọc vòng đời chỉ nhận 'draft', 'scheduled', 'open', 'closed' hoặc 'all'.");
        }

        var asOf = DateTime.UtcNow;

        // Quản trị viên đọc toàn bộ, giáo viên chỉ thấy đề mình tạo. Phạm vi áp trong SQL chứ không
        // lọc sau khi phân trang: lọc sau thì trang 1 còn ba dòng, trang 2 còn bảy, tổng số trang sai.
        var page = await _unitOfWork.Exams.GetPagedAsync(
            IsAdmin ? null : CurrentUserId,
            query.Search,
            lifecycle,
            asOf,
            query,
            cancellationToken);

        // Một lời gọi cho cả trang thay vì mỗi đề một lần: mười đề trên màn hình danh sách vốn là
        // hai mươi truy vấn đếm.
        var counters = await _unitOfWork.ExamAssignments.CountByExamsAsync(
            page.Items.Select(exam => exam.Id),
            cancellationToken);

        return Result.Success(page.Map(exam => ToDto(exam, asOf, counters)));
    }

    /// <inheritdoc />
    public async Task<Result<ExamDto>> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var found = await FindReadableAsync(id, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<ExamDto>(found.Error!, found.ErrorCode);
        }

        return Result.Success(await ToDtoAsync(found.ValueOrThrow, DateTime.UtcNow, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<ExamDto>> CreateAsync(
        SaveExamRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scope = ResolveWriteScope();
        if (scope.IsFailure)
        {
            return Result.Failure<ExamDto>(scope.Error!, scope.ErrorCode);
        }

        var isDraft = IsDraftRequest(request);

        var check = await ValidateBlueprintAsync(
            request.TotalQuestions,
            request.EasyPercentage,
            request.MediumPercentage,
            request.HardPercentage,
            request.Topic,
            scope.ValueOrThrow,
            isDraft,
            cancellationToken);

        if (check.IsFailure)
        {
            return Result.Failure<ExamDto>(check.Error!, check.ErrorCode);
        }

        var now = DateTime.UtcNow;
        var exam = new Exam
        {
            Title = request.Title.Trim(),
            Description = Normalize(request.Description),
            Topic = ExamMappingExtensions.NormalizeTopic(request.Topic),
            DurationMinutes = request.DurationMinutes,
            TotalQuestions = request.TotalQuestions,
            EasyPercentage = request.EasyPercentage,
            MediumPercentage = request.MediumPercentage,
            HardPercentage = request.HardPercentage,
            PointPerQuestion = request.PointPerQuestion,
            ShuffleQuestions = request.ShuffleQuestions,
            ShuffleAnswers = request.ShuffleAnswers,
            ShowResultAfterSubmit = request.ShowResultAfterSubmit,
            OpenAt = request.OpenAt.ToUtc(),
            CloseAt = request.CloseAt.ToUtc(),
            IsDraft = isDraft,

            // Cờ này được chốt ở bước giao bài (§4.6), nên đề mới luôn sinh ra ở mặc định tắt.
            RandomizePerStudent = false,

            PublishedAt = isDraft ? null : now,
            CreatedByUserId = CurrentUserId,
            CreatedAt = now
        };

        await _unitOfWork.Exams.AddAsync(exam, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Đề vừa tạo chưa thể có assignment nào, khỏi phải đếm.
        return Result.Success(exam.ToDto(now, assignedCount: 0, submittedCount: 0));
    }

    /// <inheritdoc />
    public async Task<Result<ExamDto>> UpdateAsync(
        int id,
        SaveExamRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var found = await FindOwnedAsync(id, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<ExamDto>(found.Error!, found.ErrorCode);
        }

        var exam = found.ValueOrThrow;
        var isDraft = IsDraftRequest(request);

        var check = await ValidateBlueprintAsync(
            request.TotalQuestions,
            request.EasyPercentage,
            request.MediumPercentage,
            request.HardPercentage,
            request.Topic,
            exam.CreatedByUserId,
            isDraft,
            cancellationToken);

        if (check.IsFailure)
        {
            return Result.Failure<ExamDto>(check.Error!, check.ErrorCode);
        }

        // Validator chỉ so thời lượng với lịch chung của đề. Những lớp có khung giờ riêng thì nó không
        // nhìn thấy, mà tăng thời lượng vượt khung của một lớp là để AttemptDeadline cắt bài cả lớp đó
        // giữa chừng. Em đã bắt đầu thì bỏ qua: hạn nộp của các em đã chốt lúc vào thi.
        var pendingWindows = await _unitOfWork.ExamAssignments.GetPendingOverrideWindowsAsync(exam.Id, cancellationToken);
        if (pendingWindows.Any(window => !AssignmentSchedule.FitsDuration(window.OpenAt, window.CloseAt, request.DurationMinutes)))
        {
            return Result.Failure<ExamDto>(
                $"Có lớp được đặt khung giờ riêng ngắn hơn {request.DurationMinutes} phút. "
                + "Hãy nới khung giờ của lớp đó trước, hoặc giữ thời lượng làm bài cũ.");
        }

        var now = DateTime.UtcNow;

        exam.Title = request.Title.Trim();
        exam.Description = Normalize(request.Description);
        exam.Topic = ExamMappingExtensions.NormalizeTopic(request.Topic);
        exam.DurationMinutes = request.DurationMinutes;
        exam.TotalQuestions = request.TotalQuestions;
        exam.EasyPercentage = request.EasyPercentage;
        exam.MediumPercentage = request.MediumPercentage;
        exam.HardPercentage = request.HardPercentage;
        exam.PointPerQuestion = request.PointPerQuestion;
        exam.ShuffleQuestions = request.ShuffleQuestions;
        exam.ShuffleAnswers = request.ShuffleAnswers;
        exam.ShowResultAfterSubmit = request.ShowResultAfterSubmit;
        exam.OpenAt = request.OpenAt.ToUtc();
        exam.CloseAt = request.CloseAt.ToUtc();
        exam.IsDraft = isDraft;
        exam.UpdatedAt = now;

        // Xuất bản lần đầu thì ghi mốc; đã xuất bản rồi thì giữ nguyên mốc cũ, vì "lần đầu công bố"
        // là một sự kiện chứ không phải một thuộc tính cập nhật theo mỗi lần sửa.
        if (!isDraft && exam.PublishedAt is null)
        {
            exam.PublishedAt = now;
        }

        _unitOfWork.Exams.Update(exam);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await ToDtoAsync(exam, now, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<ExamDto>> UpdateScheduleAsync(
        int id,
        UpdateScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var found = await FindOwnedAsync(id, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<ExamDto>(found.Error!, found.ErrorCode);
        }

        var exam = found.ValueOrThrow;

        // Cùng phép kiểm của SaveExamRequestValidator, nhưng ở đây thời lượng đến từ đề chứ không
        // từ request — validator không nhìn thấy nó nên không kiểm hộ được.
        if (exam.DurationMinutes > (request.CloseAt - request.OpenAt).TotalMinutes)
        {
            return Result.Failure<ExamDto>(
                $"Đề này cho làm bài {exam.DurationMinutes} phút, dài hơn khoảng thời gian mở đề vừa chọn. "
                + "Hãy nới lịch ra, hoặc rút ngắn thời gian làm bài ở phần sửa đề.");
        }

        var now = DateTime.UtcNow;

        exam.OpenAt = request.OpenAt.ToUtc();
        exam.CloseAt = request.CloseAt.ToUtc();
        exam.UpdatedAt = now;

        _unitOfWork.Exams.Update(exam);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await ToDtoAsync(exam, now, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var found = await FindOwnedAsync(id, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure(found.Error!, found.ErrorCode);
        }

        var exam = found.ValueOrThrow;

        var submitted = await _unitOfWork.ExamAssignments.CountSubmittedByExamAsync(exam.Id, cancellationToken);
        if (submitted > 0)
        {
            return Result.Conflict(HasSubmissionsMessage);
        }

        // Ba lệnh xoá phải cùng sống hoặc cùng chết. Xoá assignment xong mà đề còn lại thì học sinh
        // mất bài trong khi giáo viên vẫn thấy đề; xoá đề xong mà bản đề còn lại thì
        // ExamPaperQuestion trỏ vào một đề không tồn tại.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        await _unitOfWork.Exams.RemovePapersAsync(exam.Id, cancellationToken);
        await _unitOfWork.ExamAssignments.RemoveByExamAsync(exam.Id, cancellationToken);
        _unitOfWork.Exams.Remove(exam);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<BlueprintPreviewDto>> PreviewBlueprintAsync(
        BlueprintInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var scope = ResolveWriteScope();
        if (scope.IsFailure)
        {
            return Result.Failure<BlueprintPreviewDto>(scope.Error!, scope.ErrorCode);
        }

        return Result.Success(await BuildPreviewAsync(
            input.TotalQuestions,
            input.EasyPercentage,
            input.MediumPercentage,
            input.HardPercentage,
            input.Topic,
            scope.ValueOrThrow,
            cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<BlueprintPreviewDto>> GetBlueprintAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var found = await FindOwnedAsync(id, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<BlueprintPreviewDto>(found.Error!, found.ErrorCode);
        }

        var exam = found.ValueOrThrow;

        // Đối chiếu với tồn kho *hiện tại*, không phải tồn kho lúc lưu đề: một đề hợp lệ tuần trước
        // có thể không sinh nổi hôm nay vì đồng nghiệp vừa ẩn bớt câu hỏi. Đó chính là điều giáo
        // viên cần biết trước khi bấm giao bài.
        return Result.Success(await BuildPreviewAsync(
            exam.TotalQuestions,
            exam.EasyPercentage,
            exam.MediumPercentage,
            exam.HardPercentage,
            exam.Topic,
            exam.CreatedByUserId,
            cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<ExamDto>> PublishAsync(int id, CancellationToken cancellationToken = default)
    {
        var found = await FindOwnedAsync(id, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<ExamDto>(found.Error!, found.ErrorCode);
        }

        var exam = found.ValueOrThrow;

        if (!exam.IsDraft)
        {
            return Result.Conflict<ExamDto>("Bài kiểm tra này đã được xuất bản rồi.");
        }

        // Rời khỏi trạng thái nháp là lúc hai điều kiện được miễn lúc lưu nháp phải trả lại đủ.
        var check = await ValidateBlueprintAsync(
            exam.TotalQuestions,
            exam.EasyPercentage,
            exam.MediumPercentage,
            exam.HardPercentage,
            exam.Topic,
            exam.CreatedByUserId,
            isDraft: false,
            cancellationToken);

        if (check.IsFailure)
        {
            return Result.Failure<ExamDto>(check.Error!, check.ErrorCode);
        }

        if (exam.DurationMinutes > (exam.CloseAt - exam.OpenAt).TotalMinutes)
        {
            return Result.Failure<ExamDto>(
                "Thời gian làm bài dài hơn khoảng thời gian mở đề. Hãy sửa lịch trước khi xuất bản.");
        }

        var now = DateTime.UtcNow;

        exam.IsDraft = false;
        exam.PublishedAt = now;
        exam.UpdatedAt = now;

        _unitOfWork.Exams.Update(exam);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await ToDtoAsync(exam, now, cancellationToken));
    }

    /// <summary>Đề mà người gọi được <b>đọc</b>: giáo viên đọc đề mình tạo, quản trị viên đọc mọi đề.</summary>
    private async Task<Result<Exam>> FindReadableAsync(int id, CancellationToken cancellationToken)
    {
        var exam = await _unitOfWork.Exams.GetByIdAsync(id, cancellationToken);

        // Không tồn tại và không phải của mình cho ra cùng một câu trả lời: trả 403 ở nhánh thứ hai
        // là xác nhận id đó có thật (§4.2).
        if (exam is null || (!IsAdmin && exam.CreatedByUserId != CurrentUserId))
        {
            return Result.NotFound<Exam>(NotFoundMessage);
        }

        return Result.Success(exam);
    }

    /// <summary>Đề mà người gọi được <b>sửa</b>: chỉ chính giáo viên đã tạo.</summary>
    /// <remarks>
    /// Quản trị viên nhận <c>Forbidden</c> chứ không phải <c>NotFound</c>, khác với nhánh đọc: đề
    /// đó đang hiển thị trên màn hình của họ, nên báo "không tìm thấy" chỉ khiến người dùng tưởng
    /// hệ thống hỏng (§4.2).
    /// </remarks>
    private async Task<Result<Exam>> FindOwnedAsync(int id, CancellationToken cancellationToken)
    {
        if (IsAdmin)
        {
            return Result.Forbidden<Exam>(AdminCannotWriteMessage);
        }

        var exam = await _unitOfWork.Exams.GetByIdAsync(id, cancellationToken);

        if (exam is null || exam.CreatedByUserId != CurrentUserId)
        {
            return Result.NotFound<Exam>(NotFoundMessage);
        }

        return Result.Success(exam);
    }

    /// <summary>Người ra đề phải là giáo viên đã được gán môn — đó là phạm vi ngân hàng câu hỏi (§6).</summary>
    private Result<int> ResolveWriteScope()
    {
        if (IsAdmin)
        {
            return Result.Forbidden<int>(AdminCannotWriteMessage);
        }

        if (_currentUser.SubjectId is null)
        {
            return Result.Forbidden<int>(NoSubjectMessage);
        }

        return Result.Success(CurrentUserId);
    }

    /// <summary>Kiểm blueprint theo mức nghiêm ngặt tương ứng với nháp hay không.</summary>
    /// <remarks>
    /// Bản nháp bỏ qua hoàn toàn: mục đích của nháp là lưu một cấu hình chưa hoàn chỉnh, mà bắt nó
    /// hoàn chỉnh thì nút "Lưu nháp" không còn tác dụng gì.
    /// </remarks>
    private async Task<Result> ValidateBlueprintAsync(
        int totalQuestions,
        int easyPercentage,
        int mediumPercentage,
        int hardPercentage,
        string? topic,
        int ownerUserId,
        bool isDraft,
        CancellationToken cancellationToken)
    {
        if (isDraft)
        {
            return Result.Success();
        }

        var preview = await BuildPreviewAsync(
            totalQuestions,
            easyPercentage,
            mediumPercentage,
            hardPercentage,
            topic,
            ownerUserId,
            cancellationToken);

        return preview.IsValid
            ? Result.Success()
            : Result.Failure(string.Join(" ", preview.Errors));
    }

    /// <summary>
    /// Đối chiếu số câu cần với số câu có. Một chỗ duy nhất dựng ra kết quả này, để endpoint xem
    /// trước và phép kiểm lúc lưu không bao giờ nói hai câu khác nhau về cùng một cấu hình.
    /// </summary>
    private async Task<BlueprintPreviewDto> BuildPreviewAsync(
        int totalQuestions,
        int easyPercentage,
        int mediumPercentage,
        int hardPercentage,
        string? topic,
        int ownerUserId,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        var totalPercentage = easyPercentage + mediumPercentage + hardPercentage;
        if (totalPercentage != 100)
        {
            errors.Add($"Tổng tỷ lệ phải bằng 100%, hiện tại là {totalPercentage}%.");
        }

        if (totalQuestions < 1)
        {
            errors.Add("Số câu hỏi của đề phải lớn hơn 0.");
        }

        var required = BlueprintCalculator.CalculateRequiredCounts(
            totalQuestions,
            easyPercentage,
            mediumPercentage);

        // Chỉ đếm câu đang bật: câu bị ẩn cố ý không được vào đề mới (§4.5).
        var available = await _unitOfWork.Questions.CountByDifficultyAsync(
            _currentUser.SubjectId,
            ownerUserId,
            ExamMappingExtensions.NormalizeTopic(topic),
            QuestionStatus.Active,
            cancellationToken);

        foreach (var difficulty in Difficulties)
        {
            var need = required[difficulty];
            var have = available.TryGetValue(difficulty, out var count) ? count : 0;

            if (need > have)
            {
                errors.Add(
                    $"Không đủ câu {difficulty.ToDifficultyLabel()}: cần {need} nhưng ngân hàng chỉ có {have}.");
            }
        }

        return new BlueprintPreviewDto
        {
            Required = ToKeyedCounts(required),
            Available = ToKeyedCounts(available),
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private static readonly QuestionDifficulty[] Difficulties =
    [
        QuestionDifficulty.Easy,
        QuestionDifficulty.Medium,
        QuestionDifficulty.Hard
    ];

    /// <summary>Đổi khoá enum sang khoá chuỗi viết thường mà frontend đang đọc.</summary>
    private static IReadOnlyDictionary<string, int> ToKeyedCounts(
        IReadOnlyDictionary<QuestionDifficulty, int> counts) =>
        Difficulties.ToDictionary(
            difficulty => difficulty.ToDifficultyKey(),
            difficulty => counts.TryGetValue(difficulty, out var value) ? value : 0);

    private async Task<ExamDto> ToDtoAsync(Exam exam, DateTime asOf, CancellationToken cancellationToken)
    {
        var assigned = await _unitOfWork.ExamAssignments.CountByExamAsync(exam.Id, cancellationToken);
        var submitted = await _unitOfWork.ExamAssignments.CountSubmittedByExamAsync(exam.Id, cancellationToken);

        return exam.ToDto(asOf, assigned, submitted);
    }

    private static ExamDto ToDto(
        Exam exam,
        DateTime asOf,
        IReadOnlyDictionary<int, (int Assigned, int Submitted)> counters)
    {
        var counts = counters.TryGetValue(exam.Id, out var value) ? value : (Assigned: 0, Submitted: 0);
        return exam.ToDto(asOf, counts.Assigned, counts.Submitted);
    }

    /// <summary>Bản nháp khi client gửi <c>lifecycle: "draft"</c>; bỏ trống nghĩa là đề có hiệu lực thật.</summary>
    private static bool IsDraftRequest(SaveExamRequest request) =>
        string.Equals(
            request.Lifecycle?.Trim(),
            ExamMappingExtensions.DraftLifecycleValue,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Chuỗi rỗng và khoảng trắng đều thành NULL, để database chỉ có một cách biểu diễn "không có".</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
