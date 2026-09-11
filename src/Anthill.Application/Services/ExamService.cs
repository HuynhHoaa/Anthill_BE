using Anthill.Application.Common;
using Anthill.Application.DTOs.Exams;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Application.Validators;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

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

    /// <summary>Hậu tố tên của bản nhân bản — đủ để giáo viên không nhầm hai đề cùng tên trong danh sách.</summary>
    public const string CopyTitleSuffix = " (bản sao)";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IExamGeneratorService _generator;

    public ExamService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IExamGeneratorService generator)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _generator = generator;
    }

    /// <summary>Câu từ chối khi request đổi cấu hình của một đề đã có em bắt đầu làm.</summary>
    /// <remarks>
    /// Kể tên đúng những trường request vừa cố đổi, không đọc cả danh sách trường bị khoá: giáo viên cần
    /// biết mình phải trả ô nào về giá trị cũ, và cần biết lối ra là nhân bản chứ không phải thử lại.
    /// </remarks>
    private static string FrozenBlueprintMessage(int startedCount, IReadOnlyList<string> fields) =>
        $"Đã có {startedCount} học sinh bắt đầu làm bài nên cấu hình đề đã bị khoá, không đổi được "
        + $"{string.Join(", ", fields)}. Muốn dùng cấu hình khác, hãy nhân bản đề thành một đề mới rồi sửa "
        + "trên bản sao. Tên, mô tả, lịch mở/đóng và tuỳ chọn hiện kết quả vẫn sửa được như thường.";

    private static string BackToDraftMessage(int startedCount) =>
        $"Đã có {startedCount} học sinh bắt đầu làm bài nên không đưa đề về bản nháp được nữa. "
        + "Muốn dừng nhận bài, hãy dời giờ đóng bài.";

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
        return Result.Success(exam.ToDto(now, assignedCount: 0, submittedCount: 0, startedCount: 0));
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

        // Mọi thứ từ đây tới lúc ghi nằm trong một transaction mở đầu bằng hai khoá, theo đúng thứ tự này:
        //  1. Dòng đề. Hai lần sửa, hoặc một lần sửa và một lần giao bài, trên cùng một đề xếp hàng tại đây
        //     (giao bài khoá cùng dòng này). Không có nó thì lần giao có thể sinh bản đề từ cấu hình cũ đúng
        //     lúc lần sửa xoá bản đề và sinh lại theo cấu hình mới. Khoá xong thì đề được đọc lại, nên mọi
        //     phép so bên dưới là so với cấu hình thật chứ không với lần đọc kiểm quyền ở trên.
        //  2. Các dòng giao bài. Em bấm "Vào thi" phải cầm khoá chia sẻ trên dòng giao bài của mình trước khi
        //     đọc đề hay bản đề (ExamAttemptService.CreateAttemptAsync), nên hoặc em vào trước và phép đếm
        //     bên dưới thấy lượt của em, hoặc em phải chờ lần sửa này commit rồi đọc bản đề mới. Không có nó
        //     thì phép đếm nói "chưa ai làm", em nhận bản đề cũ, rồi bản đề đó bị xoá dưới tay em.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        if (await _unitOfWork.Exams.LockForEditAsync(exam.Id, cancellationToken) is null)
        {
            return Result.NotFound<ExamDto>(NotFoundMessage);
        }

        var assignments = await _unitOfWork.ExamAssignments.LockByExamAsync(exam.Id, cancellationToken);
        var startedCount = assignments.Count == 0
            ? 0
            : await _unitOfWork.ExamAssignments.CountStartedByExamAsync(exam.Id, cancellationToken);

        var plan = ExamEditPolicy.Decide(
            exam,
            request,
            hasAssignments: assignments.Count > 0,
            hasStartedAttempts: startedCount > 0);

        switch (plan.Decision)
        {
            case ExamEditDecision.RejectBackToDraft:
                return Result.Conflict<ExamDto>(BackToDraftMessage(startedCount));

            case ExamEditDecision.RejectFrozen:
                return Result.Conflict<ExamDto>(FrozenBlueprintMessage(startedCount, plan.FrozenFieldsChanged));
        }

        var regenerate = plan.Decision == ExamEditDecision.ApplyAndRegenerate;

        // Hai trường hợp được miễn phép kiểm ngân hàng, và chỉ hai:
        //  - Lưu nháp mà không phải sinh lại gì — mục đích của nháp là cất một cấu hình chưa xong. Sinh lại
        //    là rút câu thật, nên một đề đã giao mà rút về nháp kèm cấu hình dở dang vẫn phải qua phép kiểm.
        //  - Đề đã xuất bản mà cấu hình đề không đổi. Không có miễn trừ này thì chỉ cần đồng nghiệp ẩn bớt
        //    câu hỏi là giáo viên hết cả sửa tên hay dời lịch — đúng những thứ luật khoá hứa vẫn sửa được.
        var skipBankCheck = (isDraft && !regenerate)
                            || (!exam.IsDraft && !ExamEditPolicy.ChangesGeneratedPaper(exam, request));

        var check = await ValidateBlueprintAsync(
            request.TotalQuestions,
            request.EasyPercentage,
            request.MediumPercentage,
            request.HardPercentage,
            request.Topic,
            exam.CreatedByUserId,
            skipBankCheck,
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

        // Không gọi Exams.Update(exam): đề đang được theo dõi từ lúc đọc, và các dòng giao bài vừa khoá
        // đã được EF gắn vào exam.Assignments. Update() đi hết đồ thị đó, đánh dấu mọi dòng giao bài là
        // đã sửa và chép đè chúng bằng bản đọc lúc đầu request. Theo dõi thay đổi tự nhận ra cột nào đổi.
        if (regenerate)
        {
            var regenerated = await RegeneratePapersAsync(exam, assignments, cancellationToken);
            if (regenerated.IsFailure)
            {
                // Ngân hàng không sinh nổi cấu hình mới: cuộn lại cả cấu hình lẫn lệnh xoá bản đề cũ,
                // để các em vẫn cầm đúng bản đề của cấu hình đang có.
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<ExamDto>(regenerated.Error!, regenerated.ErrorCode);
            }
        }
        else if (plan.Decision == ExamEditDecision.ApplyAndDiscardPapers)
        {
            // Không sinh gì: chưa có em nào để sinh cho. Lần giao sau không tìm thấy bản đề nào nên sinh
            // mới theo cấu hình vừa lưu. Cùng lần ghi với cấu hình, nên hỏng thì cả hai cùng cuộn lại.
            await _unitOfWork.Exams.RemovePapersAsync(exam.Id, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(await ToDtoAsync(exam, now, cancellationToken));
    }

    /// <summary>Bỏ mọi bản đề đã sinh của đề rồi sinh lại cho đúng những lượt giao đang có.</summary>
    /// <remarks>
    /// Chỉ gọi khi chưa em nào bắt đầu làm, nên không lượt làm bài hay câu trả lời nào trỏ vào bản đề cũ.
    /// Hạt giống không đổi (vẫn theo id đề và id lượt giao), nên cùng một cấu hình luôn cho ra cùng một
    /// bản đề — đổi cấu hình rồi đổi lại thì các em nhận lại đúng bộ câu ban đầu.
    /// </remarks>
    private async Task<Result> RegeneratePapersAsync(
        Exam exam,
        IReadOnlyList<ExamAssignment> assignments,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.Exams.RemovePapersAsync(exam.Id, cancellationToken);

        // Lệnh xoá phải chạm database trước khi sinh: bộ sinh đề tìm bản đề có sẵn bằng một truy vấn, nên
        // bản cũ còn nằm đó thì nó trả lại chính bản cũ. Chỉ mục duy nhất trên bản đề chung cũng không cho
        // bản mới và bản cũ cùng tồn tại. Vẫn nằm trong transaction nên hỏng ở bước sau thì cuộn lại được.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await _generator.GeneratePapersAsync(exam, assignments, cancellationToken);
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

    /// <inheritdoc />
    /// <remarks>
    /// Bản sao luôn ra đời ở dạng <b>nháp</b>, kể cả khi đề gốc đã xuất bản: người ta nhân bản để sửa, và
    /// một bản sao đã xuất bản sẵn thì chỉ cần lỡ tay bấm "Giao bài" là cả lớp nhận một đề trùng. Nháp
    /// cũng miễn phép kiểm ngân hàng, nên nhân bản được cả khi ngân hàng hôm nay không còn đủ câu cho cấu
    /// hình cũ — đó thường chính là lúc giáo viên cần sửa nó.
    ///
    /// Không chép lượt giao, bản đề hay lượt làm bài nào: bản sao là một cấu hình, chưa phải một bài thi.
    /// </remarks>
    public async Task<Result<ExamDto>> DuplicateAsync(int id, CancellationToken cancellationToken = default)
    {
        // Cùng cổng với tạo đề: nhân bản là tạo một đề mới, chỉ khác là cấu hình chép từ đề cũ. Giáo viên đã
        // bị gỡ môn thì không còn ngân hàng nào để ra đề, và bản sao của họ chỉ là một bản nháp không bao giờ
        // xuất bản được. Kiểm trước khi tìm đề nên câu trả lời không để lộ id đề nào có thật.
        var scope = ResolveWriteScope();
        if (scope.IsFailure)
        {
            return Result.Failure<ExamDto>(scope.Error!, scope.ErrorCode);
        }

        var found = await FindOwnedAsync(id, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<ExamDto>(found.Error!, found.ErrorCode);
        }

        var source = found.ValueOrThrow;
        var now = DateTime.UtcNow;

        var copy = new Exam
        {
            Title = CopyTitle(source.Title),
            Description = source.Description,
            Topic = source.Topic,
            DurationMinutes = source.DurationMinutes,
            TotalQuestions = source.TotalQuestions,
            EasyPercentage = source.EasyPercentage,
            MediumPercentage = source.MediumPercentage,
            HardPercentage = source.HardPercentage,
            PointPerQuestion = source.PointPerQuestion,
            ShuffleQuestions = source.ShuffleQuestions,
            ShuffleAnswers = source.ShuffleAnswers,
            ShowResultAfterSubmit = source.ShowResultAfterSubmit,

            // Chép theo đề gốc để bản sao "giống hệt" đúng nghĩa. Chưa có lượt giao nào nên màn hình giao
            // bài vẫn đổi được cờ này như với một đề mới.
            RandomizePerStudent = source.RandomizePerStudent,

            OpenAt = source.OpenAt,
            CloseAt = source.CloseAt,
            IsDraft = true,
            PublishedAt = null,
            CreatedByUserId = source.CreatedByUserId,
            CreatedAt = now
        };

        await _unitOfWork.Exams.AddAsync(copy, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(copy.ToDto(now, assignedCount: 0, submittedCount: 0, startedCount: 0));
    }

    /// <summary>Tên bản sao: tên gốc cộng hậu tố, cắt bớt tên gốc khi tổng vượt trần độ dài cột.</summary>
    /// <remarks>
    /// Cắt tên gốc chứ không cắt hậu tố: hậu tố là thứ duy nhất phân biệt hai đề trong danh sách. Không
    /// để lại nửa cặp surrogate ở chỗ cắt, vì PostgreSQL từ chối chuỗi UTF-8 hỏng.
    /// </remarks>
    private static string CopyTitle(string title)
    {
        var room = SaveExamRequestValidator.MaxTitleLength - CopyTitleSuffix.Length;
        if (title.Length <= room)
        {
            return title + CopyTitleSuffix;
        }

        var cut = char.IsHighSurrogate(title[room - 1]) ? room - 1 : room;
        return title[..cut].TrimEnd() + CopyTitleSuffix;
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
        var started = await _unitOfWork.ExamAssignments.CountStartedByExamAsync(exam.Id, cancellationToken);

        return exam.ToDto(asOf, assigned, submitted, started);
    }

    private static ExamDto ToDto(
        Exam exam,
        DateTime asOf,
        IReadOnlyDictionary<int, (int Assigned, int Submitted, int Started)> counters)
    {
        var counts = counters.TryGetValue(exam.Id, out var value) ? value : (Assigned: 0, Submitted: 0, Started: 0);
        return exam.ToDto(asOf, counts.Assigned, counts.Submitted, counts.Started);
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
