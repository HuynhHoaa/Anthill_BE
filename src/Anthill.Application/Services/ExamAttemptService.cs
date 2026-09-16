using Anthill.Application.Common;
using Anthill.Application.DTOs.ExamAttempts;
using Anthill.Application.Interfaces.Realtime;
using Anthill.Application.Interfaces.Security;
using Anthill.Application.Interfaces.Services;
using Anthill.Application.Interfaces.UnitOfWork;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Services;

/// <summary>
/// Cài đặt <see cref="IExamAttemptService"/> — bảy endpoint làm bài của §4.7.
/// </summary>
/// <remarks>
/// Sáu điều quyết định nằm ở file này:
/// <list type="number">
/// <item><b>Đáp án đúng không rời server trong lúc làm bài.</b> Mọi thứ đi xuống học sinh đều qua
/// <c>AttemptQuestionDto</c>/<c>AttemptAnswerOptionDto</c> — hai kiểu không khai báo trường
/// <c>isCorrect</c>. Không nhánh nào ở đây chạm tới DTO của phía giáo viên (§3 ①).</item>
/// <item><b>Chấm ở server, từ bản chụp.</b> Request chỉ mang cặp câu–đáp án; điểm, số câu đúng và
/// thang điểm đều do <see cref="AttemptGrading"/> tính từ <c>ExamPaperAnswer</c> (§3 ②).</item>
/// <item><b>Đồng hồ là của server.</b> Không nhánh nào đọc thời gian từ request. Hạn giờ chốt vào
/// cột <c>ExpiresAt</c> ngay lúc bắt đầu và mọi phép so đều dùng nó (§3 ③).</item>
/// <item><b>Quyền sở hữu kiểm trước mọi việc khác</b>, và khi hỏng thì trả 404 chứ không 403 —
/// 403 là xác nhận id đó có thật, đủ để dò dần dữ liệu của cả trường (§3 ④, §4.2).</item>
/// <item><b>Request đầu tiên chạm vào một lượt quá hạn sẽ chốt sổ lượt đó.</b> Background job ở §7
/// là lưới an toàn cho trường hợp học sinh tắt trình duyệt, không phải con đường duy nhất — nếu là
/// duy nhất thì trong khoảng giữa hai lần quét, một lượt đã hết giờ vẫn còn mở cho ghi.</item>
/// <item><b>Mọi nhánh ghi chạy dưới một khoá ghi trên dòng lượt làm bài.</b> Lưu nháp, nộp bài và
/// chốt sổ hết giờ đều đọc trạng thái rồi mới quyết định ghi, mà cả ba có thể chồng lên nhau:
/// autosave 800ms một nhịp, retry sau timeout mạng, hai tab cùng mở. Đọc không khoá thì hai request
/// cùng thấy trạng thái cũ rồi cùng ghi, bên commit sau đè lên bên commit trước, và cả hai vẫn trả
/// 200 nên không ai biết bài vừa bị lùi lại một bước (§5).</item>
/// </list>
/// </remarks>
public sealed class ExamAttemptService : IExamAttemptService
{
    private const string AssignmentNotFoundMessage = "Không tìm thấy bài kiểm tra này trong danh sách của bạn.";
    private const string AttemptNotFoundMessage = "Không tìm thấy lượt làm bài này.";

    private const string PaperMissingMessage =
        "Đề của bài kiểm tra này chưa được sinh hoặc đã bị xoá. Hãy báo giáo viên phụ trách.";

    private const string NotOpenYetMessage = "Bài kiểm tra chưa mở. Hãy quay lại khi tới giờ làm bài.";

    private const string ClosedMessage = "Bài kiểm tra đã đóng nên không vào làm được nữa.";

    private const string AlreadyFinishedMessage =
        "Bạn đã nộp bài kiểm tra này rồi. Mỗi bài chỉ làm được một lần.";

    private const string ExpiredMessage = "Đã hết giờ làm bài. Hệ thống đã nộp phần bạn làm được.";

    private const string NotInProgressMessage =
        "Lượt làm bài này đã kết thúc. Xem lại ở mục kết quả của bạn.";

    // Bộ đếm ngược trên client bấm nộp NGAY khi còn 0 giây, rồi gói tin mới lên đường — nên request
    // của một lần auto-submit hợp lệ hầu như luôn chạm server sau ExpiresAt, không phải trường hợp
    // hiếm. Cửa sổ này chỉ nới cho đúng cái request mà bộ đếm ngược đã tự kích hoạt không điều kiện
    // (client không có cách nào giữ nó lại để "câu giờ" mà không làm hỏng luôn việc nộp bài); nó
    // không mở lại cửa cho một client vẫn đang chủ động trả lời sau hạn.
    private static readonly TimeSpan LateSubmitGraceWindow = TimeSpan.FromSeconds(20);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IExamGeneratorService _generator;
    private readonly IRealtimeNotificationService _realtime;

    public ExamAttemptService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IExamGeneratorService generator,
        IRealtimeNotificationService realtime)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _generator = generator;
        _realtime = realtime;
    }

    /// <summary>Danh tính lấy từ JWT, không bao giờ từ thân request (§3 quy tắc ④).</summary>
    private int CurrentStudentId => _currentUser.RequireUserId();

    /// <summary>Giờ server, cắt về độ chính xác micro giây.</summary>
    /// <remarks>
    /// PostgreSQL lưu <c>timestamptz</c> tới micro giây, còn <see cref="DateTime"/> của .NET đếm
    /// tới 100 nano giây. Không cắt ở đây thì response của lần bắt đầu — dựng từ giá trị còn trong
    /// bộ nhớ — mang bảy chữ số thập phân, trong khi mọi lần đọc sau đó chỉ còn sáu. Với một
    /// endpoint mà cả hợp đồng là "gọi lại phải trả về đúng phiên cũ", hai chuỗi <c>expiresAt</c>
    /// khác nhau là lời hứa đó bị vi phạm ngay ở chỗ dễ kiểm chứng nhất.
    /// </remarks>
    private static DateTime UtcNow()
    {
        var now = DateTime.UtcNow;

        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<StudentExamItemDto>>> ListMyExamsAsync(
        ListMyExamsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!ExamAttemptMappingExtensions.TryParseAttemptStatusFilter(query.Status, out var status))
        {
            return Result.Failure<IReadOnlyList<StudentExamItemDto>>(
                "Bộ lọc trạng thái chỉ nhận 'assigned', 'inprogress', 'submitted', 'graded', 'expired' hoặc 'all'.");
        }

        var asOf = UtcNow();

        // Phạm vi nằm trong chính câu truy vấn: repository chỉ trả về assignment của học sinh này,
        // nên không có đường nào để một bộ lọc viết sai làm lộ bài của em khác.
        var assignments = await _unitOfWork.ExamAssignments.GetForStudentAsync(CurrentStudentId, cancellationToken);

        var items = assignments
            .Select(assignment => assignment.ToStudentExamItemDto(asOf))
            .Where(item => status is null || item.Status == status.Value.ToAssignmentStatusString())
            .ToList();

        return Result.Success<IReadOnlyList<StudentExamItemDto>>(items);
    }

    /// <inheritdoc />
    public async Task<Result<StudentExamItemDto>> GetMyExamAsync(
        int assignmentId,
        CancellationToken cancellationToken = default)
    {
        var found = await FindOwnedAssignmentAsync(assignmentId, cancellationToken);

        return found.IsFailure
            ? Result.Failure<StudentExamItemDto>(found.Error!, found.ErrorCode)
            : Result.Success(found.ValueOrThrow.ToStudentExamItemDto(UtcNow()));
    }

    /// <inheritdoc />
    public async Task<Result<AttemptSessionDto>> StartAsync(
        StartAttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var found = await FindOwnedAssignmentAsync(request.AssignmentId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<AttemptSessionDto>(found.Error!, found.ErrorCode);
        }

        var assignment = found.ValueOrThrow;
        var now = UtcNow();
        var decision = DecideStart(assignment, now);

        if (decision == AttemptStartDecision.CreateNew)
        {
            // Quyết định trên chỉ dựa vào một lần đọc không khoá. CreateAttemptAsync khoá dòng giao bài,
            // đọc lại rồi quyết định lần nữa; NULL nghĩa là lần quyết định sau khoá khác lần này.
            var created = await CreateAttemptAsync(assignment, now, cancellationToken);
            if (created is not null)
            {
                return created;
            }

            // Transaction của lần tạo đã đóng lại, còn assignment giữ giá trị vừa đọc dưới khoá. Đi tiếp
            // theo quyết định mới — thường là Resume vì tab kia của chính em vừa tạo lượt. Các nhánh
            // Finalise tự mở transaction riêng nên không được chạy lồng trong transaction vừa rồi.
            decision = DecideStart(assignment, now);
        }

        var attempt = assignment.Attempt;

        switch (decision)
        {
            case AttemptStartDecision.NotOpenYet:
                return Result.Conflict<AttemptSessionDto>(NotOpenYetMessage);

            case AttemptStartDecision.Closed:
                return Result.Gone<AttemptSessionDto>(ClosedMessage);

            case AttemptStartDecision.AlreadyFinished:
                // Bài bị chốt vì hết giờ thì vào lại nhận đúng câu "hết giờ" như lưu nháp và nạp lại
                // phiên — không phụ thuộc tiến trình nền hay chính em đã chốt trước. 409 "đã nộp rồi"
                // chỉ dành cho bài em tự nộp trước giờ.
                return AttemptClosure.ClosedByDeadline(attempt!.Status, attempt.AutoSubmitted, attempt.ExpiresAt, now)
                    ? Result.Gone<AttemptSessionDto>(ExpiredMessage)
                    : Result.Conflict<AttemptSessionDto>(AlreadyFinishedMessage);

            case AttemptStartDecision.Finalise:
            {
                // Trả về một phiên còn gõ được cho một lượt đã hết giờ nghĩa là mở lại cửa sổ ghi
                // đã đóng. Chốt sổ ngay rồi mới báo hết giờ.
                var finalised = await FinaliseExpiredUnderLockAsync(attempt!, now, cancellationToken);

                return finalised.IsFailure
                    ? Result.Failure<AttemptSessionDto>(finalised.Error!, finalised.ErrorCode)
                    : Result.Gone<AttemptSessionDto>(ExpiredMessage);
            }

            case AttemptStartDecision.Resume:
                // Đây là phần khiến endpoint idempotent: không đụng vào StartedAt, ExpiresAt hay
                // bản đề. Học sinh F5 giữa giờ không được cấp thêm một giây nào.
                return await BuildSessionAsync(attempt!, now, cancellationToken);

            default:
                // Không tới được: CreateNew đã được xử lý và quyết định lại ở trên, và lần quyết định sau
                // khoá chỉ trả NULL khi nó khác CreateNew. Vẫn trả một câu có nghĩa thay vì ném lỗi 500.
                return Result.Conflict<AttemptSessionDto>(PaperMissingMessage);
        }
    }

    /// <summary>
    /// Vòng đời theo lịch của BẢN GHI GIAO BÀI, cộng trạng thái lượt đã có.
    /// </summary>
    /// <remarks>
    /// Đọc lịch chung của đề ở đây là chặn đúng những em đang trong giờ của lớp mình, hoặc mở cho những
    /// em chưa tới giờ — cả hai đều hỏng ngay giữa buổi kiểm tra.
    /// </remarks>
    private static AttemptStartDecision DecideStart(ExamAssignment assignment, DateTime now) =>
        AttemptStartPolicy.Decide(
            assignment.ResolveLifecycle(now),
            assignment.Attempt?.Status,
            assignment.Attempt?.ExpiresAt,
            now);

    /// <inheritdoc />
    public async Task<Result<AttemptSessionDto>> GetSessionAsync(
        int attemptId,
        CancellationToken cancellationToken = default)
    {
        var found = await FindOwnedAttemptAsync(attemptId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<AttemptSessionDto>(found.Error!, found.ErrorCode);
        }

        var attempt = found.ValueOrThrow;
        var now = UtcNow();

        // Bài đã nộp thì không dựng lại màn hình làm bài nữa: đưa lại một phiên gõ được cho một lượt
        // đã chấm chỉ mời thêm một lần nộp thứ hai. Xem lại từng câu là việc của §4.8.
        // Bài bị chốt vì hết giờ thì báo đúng "hết giờ" như nhánh tự chốt ngay dưới, dù tiến trình
        // nền có chốt trước lời gọi này hay không — cùng lý do như ở SaveAnswersAsync.
        if (attempt.Status != AssignmentStatus.InProgress)
        {
            return AttemptClosure.ClosedByDeadline(attempt.Status, attempt.AutoSubmitted, attempt.ExpiresAt, now)
                ? Result.Gone<AttemptSessionDto>(ExpiredMessage)
                : Result.Conflict<AttemptSessionDto>(NotInProgressMessage);
        }

        if (AttemptDeadline.IsExpired(attempt.ExpiresAt, now))
        {
            var finalised = await FinaliseExpiredUnderLockAsync(attempt, now, cancellationToken);

            return finalised.IsFailure
                ? Result.Failure<AttemptSessionDto>(finalised.Error!, finalised.ErrorCode)
                : Result.Gone<AttemptSessionDto>(ExpiredMessage);
        }

        return await BuildSessionAsync(attempt, now, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<AttemptTimeDto>> GetTimeAsync(
        int attemptId,
        CancellationToken cancellationToken = default)
    {
        // Hai truy vấn nhẹ thay cho một truy vấn nạp kèm đề, bản đề và hồ sơ học sinh: màn hình làm
        // bài gọi endpoint này đều đặn suốt buổi thi chỉ để chỉnh lại con số đếm ngược.
        var ownerId = await _unitOfWork.ExamAttempts.GetOwnerStudentIdAsync(attemptId, cancellationToken);
        if (ownerId != CurrentStudentId)
        {
            return Result.NotFound<AttemptTimeDto>(AttemptNotFoundMessage);
        }

        var attempt = await _unitOfWork.ExamAttempts.GetByIdAsync(attemptId, cancellationToken);
        if (attempt is null)
        {
            return Result.NotFound<AttemptTimeDto>(AttemptNotFoundMessage);
        }

        // Cố ý không chốt sổ lượt quá hạn ở đây. Đây là endpoint đọc, bị gọi lặp và có thể gọi song
        // song từ hai tab; để nó ghi database là biến một phép đồng bộ đồng hồ thành một cuộc đua.
        return Result.Success(attempt.ToTimeDto(UtcNow()));
    }

    /// <inheritdoc />
    public async Task<Result<SaveAnswersResponse>> SaveAnswersAsync(
        int attemptId,
        SaveAnswersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Khoá dòng lượt làm bài trước khi đọc, và giữ khoá tới lúc commit. Bộ lọc clientSeq bên
        // dưới là một phép đọc-rồi-ghi: hai nhịp autosave chồng lấn mà cùng đọc được LastAppliedSeq
        // cũ thì cả hai cùng qua được nó, và gói cũ commit sau sẽ đè lên gói mới trong khi cả hai
        // vẫn trả 200. Khoá cũng là thứ khiến hai lời gọi không thể cùng lúc thêm dòng cho một câu
        // rồi vướng unique index — cái giá của lần thua là học sinh mất thao tác giữa buổi thi.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var found = await LockOwnedAttemptAsync(attemptId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<SaveAnswersResponse>(found.Error!, found.ErrorCode);
        }

        var attempt = found.ValueOrThrow;
        var now = UtcNow();

        // Đọc dưới khoá nên đây là trạng thái thật lúc này: một lần nộp bài chạy song song thì hoặc
        // đã xong hẳn và nhìn thấy được ở đây, hoặc còn đang chờ chính khoá này.
        //
        // Lượt đã bị tiến trình nền chốt vì hết giờ vẫn phải nhận 410 như nhánh tự chốt ngay dưới.
        // Trả 409 ở đây thì cùng một gói nháp sau giờ hết nhận hai mã khác nhau tuỳ ai chốt trước.
        if (attempt.Status != AssignmentStatus.InProgress)
        {
            return AttemptClosure.ClosedByDeadline(attempt.Status, attempt.AutoSubmitted, attempt.ExpiresAt, now)
                ? Result.Gone<SaveAnswersResponse>(ExpiredMessage)
                : Result.Conflict<SaveAnswersResponse>(NotInProgressMessage);
        }

        // §5: hết giờ thì không nhận nháp mới, trả 410. Chốt sổ luôn tại đây để lần gọi sau nhận
        // được một câu trả lời nhất quán thay vì lại rơi vào đúng nhánh này. Gọi thẳng bản không tự
        // mở transaction vì khoá và transaction đã nằm sẵn trong tay từ đầu hàm.
        if (AttemptDeadline.IsExpired(attempt.ExpiresAt, now))
        {
            var finalised = await FinaliseExpiredAsync(attempt, now, cancellationToken);
            if (finalised.IsFailure)
            {
                return Result.Failure<SaveAnswersResponse>(finalised.Error!, finalised.ErrorCode);
            }

            await transaction.CommitAsync(cancellationToken);

            return Result.Gone<SaveAnswersResponse>(ExpiredMessage);
        }

        // Gói tới sai thứ tự: bỏ qua, nhưng NÓI RÕ là đã bỏ. Vẫn trả 200 vì đây không phải lỗi của
        // người dùng mà là hệ quả của độ trễ mạng, và trả lỗi chỉ khiến client thử lại đúng gói cũ.
        //
        // Trước đây nhánh này trả về đúng câu "Đã lưu bài làm." như nhánh ghi thật, nên một client
        // có bộ đếm lệch — sau F5 chẳng hạn — sẽ thấy "đã lưu" ở mọi lần gọi trong khi database
        // không nhận gì cả. Trả lại số thứ tự server đang giữ để client tự chỉnh bộ đếm.
        if (request.ClientSeq <= attempt.LastAppliedSeq)
        {
            return Result.Success(new SaveAnswersResponse
            {
                Applied = false,
                LastAppliedSeq = attempt.LastAppliedSeq,
                SavedCount = 0
            });
        }

        var paper = await ResolvePaperAsync(attempt.Assignment, cancellationToken);
        if (paper.IsFailure)
        {
            return Result.Failure<SaveAnswersResponse>(paper.Error!, paper.ErrorCode);
        }

        var questionsById = paper.ValueOrThrow.Questions.ToDictionary(question => question.Id);

        var merged = await MergeAnswersAsync(attempt, request.Answers, questionsById, now, cancellationToken);
        if (merged.IsFailure)
        {
            return Result.Failure<SaveAnswersResponse>(merged.Error!, merged.ErrorCode);
        }

        // Gán thẳng lên thực thể đang được change tracker theo dõi chứ không gọi Update(): Update()
        // đánh dấu mọi cột là đã sửa, biến một lệnh ghi hai cột thành lệnh chép đè cả dòng — kể cả
        // Status, Score và SubmittedAt — bằng giá trị đọc được lúc đầu request.
        attempt.LastAppliedSeq = request.ClientSeq;
        attempt.UpdatedAt = now;

        // Một lần ghi cho cả gói: các dòng câu trả lời và mốc LastAppliedSeq phải cùng sống hoặc
        // cùng chết. Ghi được mốc mà mất phần câu trả lời thì mọi gói sau đó đều bị coi là cũ và bỏ qua.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new SaveAnswersResponse
        {
            Applied = true,
            LastAppliedSeq = request.ClientSeq,
            SavedCount = request.Answers.Count
        });
    }

    /// <inheritdoc />
    public async Task<Result<SubmitAttemptResponse>> SubmitAsync(
        int attemptId,
        SubmitAttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // §5 đòi nộp bài idempotent, mà chốt chặn cho việc đó chỉ là phép so Status ở dưới. Đọc
        // không khoá thì hai lần retry sau timeout mạng cùng thấy InProgress, cùng chấm, cùng ghi
        // SubmittedAt của riêng mình và cùng bắn cho giáo viên một thông báo "vừa nộp bài".
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var found = await LockOwnedAttemptAsync(attemptId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<SubmitAttemptResponse>(found.Error!, found.ErrorCode);
        }

        var attempt = found.ValueOrThrow;
        var now = UtcNow();

        var paper = await ResolvePaperAsync(attempt.Assignment, cancellationToken);
        if (paper.IsFailure)
        {
            return Result.Failure<SubmitAttemptResponse>(paper.Error!, paper.ErrorCode);
        }

        // Chốt thứ tự ngay khi lấy ra: cùng một danh sách đó vừa dùng để chấm vừa dùng để đếm
        // tổng số câu, nên nó phải là một bản đã cố định chứ không phải navigation còn sống.
        var questions = paper.ValueOrThrow.Questions.OrderBy(question => question.Ordinal).ToList();
        var selections = await ReadSelectionsAsync(attempt.Id, cancellationToken);

        // Nộp lại cùng một attemptId trả về đúng bản cũ (§5). Cờ chống bấm đúp ở frontend không đủ:
        // retry sau timeout mạng vẫn tới server hai lần, và lần thứ hai không được phép chấm lại
        // trên một bộ câu trả lời có thể đã khác. Phép so này chỉ đáng tin nhờ khoá ghi ở đầu hàm:
        // nhờ nó, lần nộp kia hoặc đã xong hẳn và hiện ra ở đây, hoặc còn chưa bắt đầu.
        if (attempt.Status is AssignmentStatus.Submitted or AssignmentStatus.Graded)
        {
            return Result.Success(BuildSubmitResponse(
                attempt,
                questions.Count,
                selections.Count(entry => entry.Value.HasValue)));
        }

        if (attempt.Status == AssignmentStatus.Expired)
        {
            return Result.Gone<SubmitAttemptResponse>(ExpiredMessage);
        }

        // Bấm Nộp sau khi chuông đã reo: chấm phần đã lưu và bỏ qua phần gửi kèm muộn. Từ chối
        // thẳng thì học sinh mất trắng bài đã làm chỉ vì gói tin đi mất ba giây; còn nhận phần gửi
        // kèm thì cửa sổ ghi dài thêm đúng bằng độ trễ mạng, mà độ trễ đó client tự tạo ra được.
        var lateSubmit = AttemptDeadline.IsExpired(attempt.ExpiresAt, now);

        // lateSubmit vẫn giữ nguyên ý nghĩa "quá hạn" cho việc chấm điểm/ghi sổ ở GradeAsync bên
        // dưới — không đổi. Nhưng việc CÓ GỘP đáp án gửi kèm hay không thì cần một mốc rộng hơn một
        // chút: request tự-nộp của client chạm server trễ hơn ExpiresAt gần như luôn luôn (xem giải
        // thích ở LateSubmitGraceWindow phía trên), nên nếu gộp đúng bằng lateSubmit thì nhánh gộp
        // coi như chết, và bài cuối cùng học sinh chọn trước khi hết giờ bị rơi mất mỗi lần.
        var withinGraceWindow = now.ToUtc() <= attempt.ExpiresAt.ToUtc() + LateSubmitGraceWindow;

        // KHONG chan theo ClientSeq o day. Bo dem chi de xu ly hai goi luu nhap den sai thu tu;
        // con nop bai la loi tuyen bo cuoi cung cua hoc sinh, khong co goi nao "moi hon" dang tren
        // duong toi. Neu client vua tai lai trang thi bo dem cua no bat dau lai tu dau, va chan o
        // day nghia la cham diem tren dap an CU roi tra ve 200 kem mot con diem — hoc sinh mat bai
        // vinh vien ma khong ai bao gi.
        if ((!lateSubmit || withinGraceWindow) && request.Answers is { Count: > 0 })
        {
            var questionsById = questions.ToDictionary(question => question.Id);

            var merged = await MergeAnswersAsync(attempt, request.Answers, questionsById, now, cancellationToken);
            if (merged.IsFailure)
            {
                return Result.Failure<SubmitAttemptResponse>(merged.Error!, merged.ErrorCode);
            }

            // Bản đọc từ database chưa biết những câu vừa gộp vì chúng còn nằm trong bộ nhớ. Phản
            // chiếu lại đúng phần đó thay vì ghi xuống rồi đọc lên, để cả việc nộp bài vẫn gọn
            // trong đúng một lần ghi.
            foreach (var answer in request.Answers)
            {
                selections[answer.QuestionId] = answer.AnswerId;
            }

            if (request.ClientSeq is { } clientSeq)
            {
                attempt.LastAppliedSeq = clientSeq;
            }
        }

        var graded = await GradeAsync(attempt, questions, selections, now, lateSubmit, cancellationToken);
        if (graded.IsFailure)
        {
            return graded;
        }

        // Đọc số bài đã nộp TRONG transaction, trước khi commit. Đọc sau commit thì đó là một
        // lượt đi database nằm ngoài mọi lớp bảo vệ: một trục trặc thoáng qua ở đó sẽ biến một bài
        // vừa chấm xong thành lỗi 500 gửi về cho học sinh — bài đã lưu, điểm đã có, mà màn hình
        // báo hỏng. Sau commit chỉ còn đúng một việc là bắn sự kiện, mà việc đó tự nuốt lỗi.
        var submittedCount = await _unitOfWork.ExamAssignments.CountSubmittedByExamAsync(
            attempt.Assignment.ExamId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // SAU commit, không sớm hơn một dòng nào: giáo viên nhận thông báo về một bài đã thực sự
        // nằm trong database, không phải về một transaction có thể cuộn lại ngay sau đó (§7).
        await PublishSubmittedAsync(attempt, submittedCount, cancellationToken);

        return graded;
    }

    /// <summary>Báo cho cả học sinh lẫn giáo viên về một bài vừa được hệ thống tự nộp.</summary>
    /// <remarks>
    /// Đọc lại bản ghi kèm quan hệ vì <c>GetExpiredInProgressAsync</c> chỉ nạp những gì cần cho
    /// việc chấm. Lượt đọc này nằm ngoài mọi transaction, nhưng nó chỉ phục vụ thông báo và
    /// <see cref="IRealtimeNotificationService"/> tự nuốt lỗi, nên hỏng ở đây không kéo theo gì —
    /// bài đã được chấm và ghi xong từ trước.
    /// </remarks>
    private async Task PublishAutoSubmittedAsync(ExamAttempt attempt, CancellationToken cancellationToken)
    {
        var detail = await _unitOfWork.ExamAttempts.GetSessionAsync(attempt.Id, cancellationToken);
        if (detail is null)
        {
            return;
        }

        var exam = detail.Assignment.Exam;
        var student = detail.Assignment.Student;
        var submittedAt = (detail.SubmittedAt ?? DateTime.UtcNow).ToUtc();

        var submittedCount = await _unitOfWork.ExamAssignments.CountSubmittedByExamAsync(
            exam.Id, cancellationToken);

        await _realtime.AutoSubmittedAsync(
            detail.Assignment.StudentId,
            new AutoSubmittedStudentEvent
            {
                ExamId = exam.Id,
                AttemptId = detail.Id,
                ExamTitle = exam.Title,
                SubmittedAt = submittedAt,

                // Điểm chỉ đi kèm khi giáo viên cho xem ngay — cùng quy tắc §3 ⑥ áp ở mọi nơi khác.
                Score = exam.ShowResultAfterSubmit ? detail.Score : null
            },
            exam.CreatedByUserId,
            new AutoSubmittedTeacherEvent
            {
                ExamId = exam.Id,
                AttemptId = detail.Id,
                StudentId = detail.Assignment.StudentId,
                StudentName = student?.FullName ?? string.Empty,
                ExamTitle = exam.Title,
                Score = detail.Score,
                MaxScore = detail.MaxScore,
                SubmittedAt = submittedAt,
                SubmittedCount = submittedCount
            },
            cancellationToken);
    }

    /// <summary>Báo cho giáo viên biết có học sinh vừa nộp bài.</summary>
    /// <remarks>
    /// Màn hình theo dõi của giáo viên dùng sự kiện này để tự tăng <c>submittedCount</c> mà không
    /// phải hỏi lại server, nên con số đó đi kèm ngay trong payload.
    ///
    /// Không bọc try/catch ở đây: phần nuốt lỗi nằm trong bản cài đặt realtime, đúng một chỗ, để
    /// mọi nơi gọi nó đều được bảo vệ như nhau thay vì mỗi nơi tự nhớ.
    /// </remarks>
    private async Task PublishSubmittedAsync(
        ExamAttempt attempt,
        int submittedCount,
        CancellationToken cancellationToken)
    {
        var exam = attempt.Assignment.Exam;
        var student = attempt.Assignment.Student;

        await _realtime.ExamSubmittedAsync(
            exam.CreatedByUserId,
            new ExamSubmittedEvent
            {
                ExamId = exam.Id,
                AttemptId = attempt.Id,
                StudentId = attempt.Assignment.StudentId,
                StudentName = student?.FullName ?? string.Empty,
                ExamTitle = exam.Title,
                Score = attempt.Score,
                MaxScore = attempt.MaxScore,
                SubmittedAt = (attempt.SubmittedAt ?? DateTime.UtcNow).ToUtc(),
                SubmittedCount = submittedCount
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<int>> AutoSubmitExpiredAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            return Result.Success(0);
        }

        var now = UtcNow();
        var expired = await _unitOfWork.ExamAttempts.GetExpiredInProgressAsync(now, batchSize, cancellationToken);
        var finalised = 0;

        foreach (var attempt in expired)
        {
            // Mỗi lượt một transaction: bản đề bị xoá của một em là chuyện của riêng em đó, ba mươi
            // chín bài còn lại vẫn phải được chấm trong lượt quét này. Đếm theo giá trị trả về chứ
            // không theo IsSuccess — lượt mà chính học sinh vừa nộp xong trong lúc quét không phải
            // là việc do job này làm.
            var outcome = await FinaliseExpiredUnderLockAsync(attempt, now, cancellationToken);

            if (outcome.IsSuccess && outcome.ValueOrThrow)
            {
                finalised++;

                // Sự kiện AutoSubmitted của §7: hai payload cho hai vai trò. Học sinh biết bài
                // mình đã được nộp hộ, giáo viên biết thêm em nào và số bài đã nộp.
                //
                // Không có nó thì tiến trình nền chấm bài trong im lặng: em mở máy lên và thấy
                // một kết quả xuất hiện từ đâu không rõ, còn màn hình theo dõi của giáo viên vẫn
                // đứng nguyên cho tới lần tải trang kế tiếp.
                await PublishAutoSubmittedAsync(attempt, cancellationToken);
            }
        }

        return Result.Success(finalised);
    }

    /// <summary>Tạo lượt mới: sinh đề nếu chưa có, chốt đồng hồ, chốt thang điểm.</summary>
    /// <remarks>
    /// <para>
    /// Cả ba việc nằm trong một transaction. Tách ra thì có lúc tồn tại một lượt làm bài trỏ vào một
    /// bản đề không có thật — học sinh mở bài ra và thấy một trang trắng, giữa giờ kiểm tra.
    /// </para>
    /// <para>
    /// Câu lệnh <b>đầu tiên</b> của transaction là khoá dòng giao bài, trước mọi lần đọc đề hay bản đề.
    /// <c>ExamService.UpdateAsync</c> khoá ghi đúng các dòng này rồi mới đếm lượt làm bài và sinh lại
    /// bản đề. Trước đây hàm này đọc đề và bản đề không khoá, chỉ lần INSERT lượt làm bài mới phải chờ
    /// (khoá khoá ngoại): em đọc bản đề cũ, lần sửa xoá nó và commit, rồi lượt của em vẫn được ghi — trỏ
    /// vào một bản đề không còn, trong khi màn hình em đang hiện đúng bộ câu đã bị xoá. Giờ thì hoặc lần
    /// sửa commit trước và em đọc đề, bản đề của cấu hình mới; hoặc em ghi lượt xong trước và lần sửa thấy
    /// "đã có em làm" nên khoá cấu hình (409).
    /// </para>
    /// <para>
    /// Trả NULL khi, đọc lại dưới khoá, việc phải làm không còn là tạo lượt mới (tab kia vừa tạo lượt, lần
    /// sửa vừa rút đề về nháp, lịch vừa đổi): người gọi quyết định lại trên chính bản vừa đọc.
    /// </para>
    /// </remarks>
    private async Task<Result<AttemptSessionDto>?> CreateAttemptAsync(
        ExamAssignment assignment,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var locked = await _unitOfWork.ExamAssignments.LockForStartAsync(assignment.Id, cancellationToken);
        if (locked is null || locked.StudentId != CurrentStudentId)
        {
            // Giáo viên thu hồi bài trong lúc em chờ khoá.
            return Result.NotFound<AttemptSessionDto>(AssignmentNotFoundMessage);
        }

        if (DecideStart(locked, now) != AttemptStartDecision.CreateNew)
        {
            return null;
        }

        // Đọc SAU khi đã cầm khoá: thời lượng cho ExpiresAt và cấu hình cho bộ sinh đề phải là của đề như
        // nó đang nằm trong database, không phải của lần đọc kiểm quyền ở đầu request.
        var exam = locked.Exam;

        // Bản đề thường đã có sẵn từ lúc giao bài; hàm này chỉ thật sự sinh khi nó vắng mặt — ví dụ
        // đề chung của cả lớp bị xoá theo một assignment nào đó rồi cần dựng lại.
        var generated = await _generator.GetOrCreatePaperAsync(exam, locked, cancellationToken);
        if (generated.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<AttemptSessionDto>(generated.Error!, generated.ErrorCode);
        }

        // Ghi bản đề trước để nó có id thật, rồi đọc lại theo đúng một đường — nhờ vậy nhánh "vừa
        // sinh" và nhánh "đã có sẵn" không cần hai cách lấy câu hỏi khác nhau.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var paper = await _unitOfWork.Exams.GetPaperWithQuestionsAsync(generated.ValueOrThrow.Id, cancellationToken);
        if (paper is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<AttemptSessionDto>(PaperMissingMessage, ErrorCode.Conflict);
        }

        var attempt = new ExamAttempt
        {
            AssignmentId = locked.Id,
            StartedAt = now,
            // Hạn giờ chặn theo mốc đóng bài THẬT của em này: lớp chiều làm bài lúc lớp sáng đã
            // đóng từ lâu, và lấy mốc chung sẽ cắt bài của lớp chiều ngay khi vừa bắt đầu.
            ExpiresAt = AttemptDeadline.Resolve(
                now,
                exam.DurationMinutes,
                AssignmentSchedule.Resolve(locked).CloseAt),
            Status = AssignmentStatus.InProgress,

            // Thang điểm chốt ngay lúc bắt đầu và đọc từ bản đề, không nhân TotalQuestions với
            // PointPerQuestion: giáo viên sửa điểm mỗi câu giữa buổi thi không được đổi thang điểm
            // của một bài đang làm dở.
            MaxScore = AttemptGrading.MaxScoreOf(paper.Questions),

            LastAppliedSeq = 0,
            CreatedAt = now
        };

        await _unitOfWork.ExamAttempts.AddAsync(attempt, cancellationToken);

        // Unique index trên AssignmentId là chốt chặn cuối cùng: hai tab bấm "Vào thi" cùng lúc thì
        // cả hai cùng thấy "chưa có lượt nào", và chỉ ràng buộc ở database mới chặn được bản ghi
        // thứ hai. Lần gọi thua cuộc nhận lỗi và thử lại là vào được nhánh Resume.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Navigation này chưa được gán vì attempt vừa dựng bằng tay; BuildSession cần nó để đọc
        // tiêu đề và thời lượng của đề.
        attempt.Assignment = locked;

        return Result.Success(BuildSession(attempt, paper, new Dictionary<int, int>(), now));
    }

    /// <summary>Dựng phiên làm bài cho một lượt đang chạy.</summary>
    private async Task<Result<AttemptSessionDto>> BuildSessionAsync(
        ExamAttempt attempt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var paper = await ResolvePaperAsync(attempt.Assignment, cancellationToken);
        if (paper.IsFailure)
        {
            return Result.Failure<AttemptSessionDto>(paper.Error!, paper.ErrorCode);
        }

        var selections = await ReadSelectionsAsync(attempt.Id, cancellationToken);

        // Câu chưa chọn biến mất khỏi từ điển thay vì mang giá trị null — đúng hợp đồng của
        // AttemptSessionDto.SavedAnswers.
        var saved = selections
            .Where(entry => entry.Value.HasValue)
            .ToDictionary(entry => entry.Key, entry => entry.Value!.Value);

        return Result.Success(BuildSession(attempt, paper.ValueOrThrow, saved, now));
    }

    private static AttemptSessionDto BuildSession(
        ExamAttempt attempt,
        ExamPaper paper,
        IReadOnlyDictionary<int, int> savedAnswers,
        DateTime now)
    {
        var exam = attempt.Assignment.Exam;

        return new AttemptSessionDto
        {
            AttemptId = attempt.Id,
            AssignmentId = attempt.AssignmentId,
            ExamId = exam.Id,
            Title = exam.Title,
            Description = exam.Description ?? string.Empty,
            DurationMinutes = exam.DurationMinutes,
            StartedAt = attempt.StartedAt.ToUtc(),
            ExpiresAt = attempt.ExpiresAt.ToUtc(),
            ServerNow = now.ToUtc(),
            RemainingSeconds = AttemptDeadline.RemainingSeconds(attempt.ExpiresAt, now),
            Status = attempt.Status.ToAssignmentStatusString(),
            TotalQuestions = paper.Questions.Count,
            MaxScore = attempt.MaxScore,
            Questions = paper.Questions
                .OrderBy(question => question.Ordinal)
                .Select(question => question.ToAttemptQuestionDto())
                .ToList(),
            SavedAnswers = savedAnswers,
            LastAppliedSeq = attempt.LastAppliedSeq
        };
    }

    /// <summary>
    /// Gộp những câu vừa gửi vào bài làm đã lưu — ngữ nghĩa MERGE của §4.7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hai phép kiểm ở đầu hàm là chỗ §5 đòi trả <b>422</b> chứ không phải 400: cú pháp request
    /// đúng, chỉ có nội dung là vô nghĩa. Và tuyệt đối không "âm thầm bỏ qua" — một câu trả lời rơi
    /// mất trong im lặng là một câu bị chấm sai mà không ai biết để hỏi.
    /// </para>
    /// <para>
    /// Chỉ đọc đúng những câu client vừa gửi. Nạp cả bộ rồi ghi đè sẽ biến MERGE thành REPLACE, và
    /// mỗi nhịp autosave một câu sẽ xoá sạch phần còn lại của bài.
    /// </para>
    /// </remarks>
    private async Task<Result> MergeAnswersAsync(
        ExamAttempt attempt,
        IReadOnlyList<AnswerSelectionDto> answers,
        IReadOnlyDictionary<int, ExamPaperQuestion> questionsById,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var answer in answers)
        {
            if (!questionsById.TryGetValue(answer.QuestionId, out var question))
            {
                return Result.Unprocessable($"Câu hỏi {answer.QuestionId} không thuộc đề của lượt làm bài này.");
            }

            if (answer.AnswerId is { } answerId && question.Answers.All(option => option.Id != answerId))
            {
                return Result.Unprocessable($"Đáp án {answerId} không thuộc câu hỏi {answer.QuestionId}.");
            }
        }

        if (answers.Count == 0)
        {
            return Result.Success();
        }

        var existing = (await _unitOfWork.StudentAnswers.GetByAttemptAndQuestionsAsync(
                attempt.Id,
                answers.Select(answer => answer.QuestionId),
                cancellationToken))
            .ToDictionary(row => row.ExamPaperQuestionId);

        foreach (var answer in answers)
        {
            if (!existing.TryGetValue(answer.QuestionId, out var row))
            {
                await _unitOfWork.StudentAnswers.AddAsync(
                    new StudentAnswer
                    {
                        AttemptId = attempt.Id,
                        ExamPaperQuestionId = answer.QuestionId,
                        SelectedAnswerId = answer.AnswerId,
                        CreatedAt = now
                    },
                    cancellationToken);

                continue;
            }

            if (row.SelectedAnswerId == answer.AnswerId)
            {
                continue;
            }

            row.SelectedAnswerId = answer.AnswerId;
            row.UpdatedAt = now;
            _unitOfWork.StudentAnswers.Update(row);
        }

        return Result.Success();
    }

    /// <summary>Chấm và chốt sổ một lượt.</summary>
    /// <remarks>
    /// Một lần <c>SaveChangesAsync</c> ôm trọn: các dòng câu trả lời vừa gộp, điểm của lượt và thông
    /// báo cho giáo viên. Hàm này không tự mở transaction và cũng không commit — nó luôn chạy bên
    /// trong transaction đang giữ khoá ghi của lượt, và người mở transaction đó mới là người biết
    /// khi nào cả nghiệp vụ đã xong.
    /// </remarks>
    private async Task<Result<SubmitAttemptResponse>> GradeAsync(
        ExamAttempt attempt,
        IReadOnlyList<ExamPaperQuestion> questions,
        IReadOnlyDictionary<int, int?> selections,
        DateTime now,
        bool autoSubmitted,
        CancellationToken cancellationToken)
    {
        var result = AttemptGrading.Grade(questions, selections);

        // Trắc nghiệm một đáp án đúng thì chấm xong ngay trong lời gọi này, nên không có khoảng
        // giữa "đã nộp" và "đã chấm" để đi qua. Submitted vẫn còn trong enum cho những loại câu hỏi
        // cần người chấm, thứ mà §8 xếp ra ngoài phạm vi v1.
        // Gán thẳng là đủ để EF ghi đúng những cột này vì thực thể đang được change tracker theo dõi;
        // gọi Update() sẽ đánh dấu mọi cột là đã sửa và biến lệnh ghi thành một lần chép đè cả dòng.
        attempt.Status = AssignmentStatus.Graded;
        attempt.SubmittedAt = now;
        attempt.Score = result.Score;
        attempt.CorrectCount = result.CorrectCount;
        attempt.MaxScore = result.MaxScore;
        attempt.AutoSubmitted = autoSubmitted;
        attempt.UpdatedAt = now;

        await AddSubmissionNotificationsAsync(attempt, autoSubmitted, now, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(BuildSubmitResponse(attempt, questions.Count, result.AnsweredCount));
    }

    /// <summary>Chốt sổ một lượt đã quá hạn dưới khoá ghi, trong transaction của riêng nó.</summary>
    /// <remarks>
    /// <para>
    /// Trả về <c>false</c> khi tới lúc cầm được khoá thì lượt đã được một đường khác chốt xong: học
    /// sinh bấm Nộp đúng lúc chuông reo trong khi tab thứ hai vừa nạp lại phiên, hoặc background job
    /// quét trúng đúng lượt đó. Chấm lại lúc ấy không hề vô hại — nó ghi đè <c>SubmittedAt</c> và cờ
    /// <c>AutoSubmitted</c> của lần nộp thật, rồi bồi cho giáo viên một thông báo thứ hai về cùng
    /// một bài, còn học sinh thì nhận tin "đã hết giờ" dù em bấm nộp kịp.
    /// </para>
    /// <para>
    /// Mọi lời gọi đều truyền vào một lượt đã nạp kèm assignment. Change tracker định danh theo khoá
    /// chính nên truy vấn khoá trả lại đúng đối tượng đó, giữ nguyên các navigation đã có.
    /// </para>
    /// </remarks>
    private async Task<Result<bool>> FinaliseExpiredUnderLockAsync(
        ExamAttempt attempt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var locked = await _unitOfWork.ExamAttempts.LockForUpdateAsync(attempt.Id, cancellationToken);
        if (locked is null || locked.Status != AssignmentStatus.InProgress)
        {
            return Result.Success(false);
        }

        var finalised = await FinaliseExpiredAsync(locked, now, cancellationToken);
        if (finalised.IsFailure)
        {
            return Result.Failure<bool>(finalised.Error!, finalised.ErrorCode);
        }

        await transaction.CommitAsync(cancellationToken);

        return Result.Success(true);
    }

    /// <summary>Chốt sổ một lượt đã quá hạn: chấm phần đã lưu, đánh dấu hệ thống nộp thay.</summary>
    /// <remarks>Chỉ gọi khi đã cầm khoá ghi của lượt; transaction và lệnh commit là của người gọi.</remarks>
    private async Task<Result> FinaliseExpiredAsync(
        ExamAttempt attempt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var paper = await ResolvePaperAsync(attempt.Assignment, cancellationToken);
        if (paper.IsFailure)
        {
            return Result.Failure(paper.Error!, paper.ErrorCode);
        }

        var selections = await ReadSelectionsAsync(attempt.Id, cancellationToken);

        var graded = await GradeAsync(
            attempt,
            paper.ValueOrThrow.Questions.OrderBy(question => question.Ordinal).ToList(),
            selections,
            now,
            autoSubmitted: true,
            cancellationToken);

        return graded.IsFailure ? Result.Failure(graded.Error!, graded.ErrorCode) : Result.Success();
    }

    /// <summary>
    /// Ba con số nhạy cảm chỉ đi ra khi giáo viên bật <c>showResultAfterSubmit</c> (§3 quy tắc ⑥).
    /// </summary>
    /// <remarks>
    /// Chặn ở đây chứ không ở controller: mọi đường nộp bài — bấm nộp, nộp lại, hệ thống nộp thay —
    /// đều đi qua đúng hàm này, nên không nhánh nào quên mất phép kiểm.
    /// </remarks>
    private static SubmitAttemptResponse BuildSubmitResponse(
        ExamAttempt attempt,
        int totalQuestions,
        int answeredCount)
    {
        var exam = attempt.Assignment.Exam;
        var canSeeScore = exam.ShowResultAfterSubmit;

        return new SubmitAttemptResponse
        {
            AttemptId = attempt.Id,
            ExamId = exam.Id,
            Status = attempt.Status.ToAssignmentStatusString(),
            SubmittedAt = (attempt.SubmittedAt ?? attempt.ExpiresAt).ToUtc(),
            AutoSubmitted = attempt.AutoSubmitted,
            TotalQuestions = totalQuestions,
            AnsweredCount = answeredCount,
            MaxScore = attempt.MaxScore,
            ShowResultAfterSubmit = exam.ShowResultAfterSubmit,
            Score = canSeeScore ? attempt.Score : null,
            CorrectCount = canSeeScore ? attempt.CorrectCount : null
        };
    }

    /// <summary>Bản ghi thông báo cho lần nộp bài này, nằm trong cùng lần ghi với điểm số.</summary>
    /// <remarks>
    /// Sinh thông báo là việc của backend và phải cùng transaction với hành động nghiệp vụ (§7).
    /// Việc đẩy sự kiện realtime <c>ExamSubmitted</c>/<c>AutoSubmitted</c> qua SignalR thuộc bước 8
    /// và phải xảy ra <b>sau khi commit</b> — bắn trước thì có lúc giáo viên nhận tin về một bài mà
    /// transaction ngay sau đó cuộn lại.
    /// </remarks>
    private async Task AddSubmissionNotificationsAsync(
        ExamAttempt attempt,
        bool autoSubmitted,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var assignment = attempt.Assignment;
        var exam = assignment.Exam;
        var studentName = assignment.Student?.FullName ?? "Một học sinh";

        var notifications = new List<Notification>
        {
            new()
            {
                RecipientUserId = exam.CreatedByUserId,
                Type = autoSubmitted ? NotificationType.AutoSubmitted : NotificationType.ExamSubmitted,
                Title = autoSubmitted ? "Hệ thống đã nộp bài thay học sinh" : "Học sinh vừa nộp bài",
                Message = autoSubmitted
                    ? $"{studentName} hết giờ làm bài \"{exam.Title}\" nên hệ thống đã nộp phần em làm được."
                    : $"{studentName} vừa nộp bài \"{exam.Title}\".",
                Link = "/giao-vien/ket-qua",
                TargetType = nameof(ExamAttempt),
                TargetId = attempt.Id,
                IsRead = false,
                CreatedAt = now
            }
        };

        // §7: sự kiện AutoSubmitted tới cả hai vai trò với hai nội dung khác nhau. Giáo viên cần
        // biết em nào không kịp; học sinh cần biết bài mình vẫn được ghi nhận chứ không mất trắng —
        // đó là câu hỏi đầu tiên của một người vừa bị cắt ngang giữa buổi thi.
        if (autoSubmitted)
        {
            notifications.Add(new Notification
            {
                RecipientUserId = assignment.StudentId,
                Type = NotificationType.AutoSubmitted,
                Title = "Bài kiểm tra đã hết giờ",
                Message = $"Hết giờ làm bài \"{exam.Title}\". Hệ thống đã nộp phần bạn làm được.",
                Link = "/hoc-sinh/ket-qua",
                TargetType = nameof(ExamAttempt),
                TargetId = attempt.Id,
                IsRead = false,
                CreatedAt = now
            });
        }

        await _unitOfWork.Notifications.AddRangeAsync(notifications, cancellationToken);
    }

    /// <summary>Bản đề của một assignment, đã nạp câu hỏi và phương án theo đúng thứ tự đã chốt.</summary>
    /// <remarks>
    /// Bản riêng hay bản chung do cờ <c>randomizePerStudent</c> của đề quyết định. Đọc hai lượt —
    /// tìm bản đề rồi nạp nội dung — thay vì viết một truy vấn riêng cho mỗi nhánh, để cả service
    /// chỉ có đúng một đường lấy câu hỏi và không nhánh nào lỡ trả về bản đề của lớp khác.
    /// </remarks>
    private async Task<Result<ExamPaper>> ResolvePaperAsync(
        ExamAssignment assignment,
        CancellationToken cancellationToken)
    {
        var exam = assignment.Exam;

        var paper = exam.RandomizePerStudent
            ? await _unitOfWork.Exams.GetPaperByAssignmentAsync(assignment.Id, cancellationToken)
            : await _unitOfWork.Exams.GetSharedPaperAsync(exam.Id, cancellationToken);

        if (paper is null)
        {
            return Result.Failure<ExamPaper>(PaperMissingMessage, ErrorCode.Conflict);
        }

        var loaded = await _unitOfWork.Exams.GetPaperWithQuestionsAsync(paper.Id, cancellationToken);

        return loaded is null
            ? Result.Failure<ExamPaper>(PaperMissingMessage, ErrorCode.Conflict)
            : Result.Success(loaded);
    }

    /// <summary>Bản sao ghi được của map <c>questionId → answerId</c> đã lưu.</summary>
    private async Task<Dictionary<int, int?>> ReadSelectionsAsync(int attemptId, CancellationToken cancellationToken) =>
        new(await _unitOfWork.StudentAnswers.GetSelectionMapAsync(attemptId, cancellationToken));

    /// <summary>Bản ghi giao bài của <b>chính người đang gọi</b>.</summary>
    /// <remarks>
    /// Không tồn tại và không phải của mình đều trả <see cref="ErrorCode.NotFound"/>. Phân biệt hai
    /// trường hợp bằng 403 là xác nhận id đó có thật, và với khoá tự tăng thì một vòng lặp là đủ để
    /// dựng lại danh sách bài của cả trường (§4.2).
    /// </remarks>
    private async Task<Result<ExamAssignment>> FindOwnedAssignmentAsync(
        int assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await _unitOfWork.ExamAssignments.GetDetailAsync(assignmentId, cancellationToken);

        return assignment is null || assignment.StudentId != CurrentStudentId
            ? Result.NotFound<ExamAssignment>(AssignmentNotFoundMessage)
            : Result.Success(assignment);
    }

    /// <summary>Lượt làm bài của <b>chính người đang gọi</b>, nạp kèm assignment, đề và hồ sơ học sinh.</summary>
    private async Task<Result<ExamAttempt>> FindOwnedAttemptAsync(
        int attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await _unitOfWork.ExamAttempts.GetSessionAsync(attemptId, cancellationToken);

        return attempt is null || attempt.Assignment.StudentId != CurrentStudentId
            ? Result.NotFound<ExamAttempt>(AttemptNotFoundMessage)
            : Result.Success(attempt);
    }

    /// <summary>Cũng lượt làm bài đó, nhưng khoá dòng lại trước khi đọc. Dành cho các nhánh ghi.</summary>
    /// <remarks>
    /// Kiểm quyền trước rồi mới khoá: một id đoán bừa không được phép giữ dòng của người khác, dù
    /// chỉ trong khoảnh khắc. Truy vấn nạp ngữ cảnh chạy <i>sau</i> khi đã cầm khoá và trả về đúng
    /// thực thể vừa khoá, nên mọi giá trị đem ra quyết định đều là giá trị của bản đã khoá chứ không
    /// phải của một lần đọc trước đó.
    /// </remarks>
    private async Task<Result<ExamAttempt>> LockOwnedAttemptAsync(
        int attemptId,
        CancellationToken cancellationToken)
    {
        var ownerId = await _unitOfWork.ExamAttempts.GetOwnerStudentIdAsync(attemptId, cancellationToken);
        if (ownerId != CurrentStudentId)
        {
            return Result.NotFound<ExamAttempt>(AttemptNotFoundMessage);
        }

        if (await _unitOfWork.ExamAttempts.LockForUpdateAsync(attemptId, cancellationToken) is null)
        {
            return Result.NotFound<ExamAttempt>(AttemptNotFoundMessage);
        }

        return await FindOwnedAttemptAsync(attemptId, cancellationToken);
    }
}
