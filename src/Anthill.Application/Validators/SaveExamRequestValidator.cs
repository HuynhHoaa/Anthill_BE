using Anthill.Application.Common;
using Anthill.Application.DTOs.Exams;
using Anthill.Application.Mappings;
using Anthill.Domain.Enums;
using FluentValidation;

namespace Anthill.Application.Validators;

/// <summary>
/// Quy tắc lấy từ <c>examService.validatePayload</c> của frontend, cộng hai điều frontend chưa
/// thực sự chặn.
/// </summary>
/// <remarks>
/// <para>
/// Ở đây chỉ có những kiểm tra <b>tự nó đứng được</b> — nhìn vào request là kết luận được. Hai
/// điều kiện nặng còn lại (tổng ba tỷ lệ bằng 100, ngân hàng đủ câu) nằm ở
/// <c>ExamService</c>, vì chúng chỉ áp dụng cho đề <i>không phải nháp</i> và điều kiện thứ hai
/// còn phải đếm database.
/// </para>
/// <para>Hai điều frontend chưa chặn thật:</para>
/// <list type="bullet">
/// <item><b><c>durationMinutes</c> không được dài hơn cửa sổ <c>openAt..closeAt</c></b> (§5).
/// Giao diện mới chỉ hiện một dòng cảnh báo rồi vẫn cho lưu. Cho làm bài 90 phút trong cửa sổ mở
/// 30 phút nghĩa là mọi học sinh đều bị cắt giữa chừng.</item>
/// <item><b><c>lifecycle</c> chỉ nhận <c>"draft"</c>.</b> Ba giá trị còn lại là dữ liệu dẫn xuất
/// từ mốc thời gian; nhận chúng là cho phép ép đề sang trạng thái mâu thuẫn với chính lịch của nó.</item>
/// </list>
/// </remarks>
public sealed class SaveExamRequestValidator : AbstractValidator<SaveExamRequest>
{
    public const int MaxTitleLength = 256;
    /// <summary>Khớp cột <c>Exams.Description</c> (1000); trần cao hơn chỉ đẩy lỗi xuống database thành 500.</summary>
    public const int MaxDescriptionLength = 1000;
    public const int MaxTopicLength = 128;

    /// <summary>Cùng trần với frontend: quá 5 tiếng thì không còn là một buổi kiểm tra.</summary>
    public const int MaxDurationMinutes = 300;

    /// <summary>Trần thực dụng cho một đề trắc nghiệm; cũng chặn một request đòi sinh 100000 câu.</summary>
    public const int MaxTotalQuestions = 500;

    /// <summary>
    /// Trần cứng của điểm một câu. Cố ý <b>không</b> theo tổng điểm chuẩn (<see cref="ExamScoring.TotalScore"/> = 10):
    /// đề cũ chấm trên 100 có câu nhiều hơn 10 điểm, và hạ trần xuống 10 sẽ làm những đề đó không sửa nổi cả tên
    /// hay mô tả (mọi lần lưu đều đi qua phép kiểm này). Giao diện nhắc giáo viên về chuẩn 10, server chỉ chặn
    /// con số vô lý. Cũng nằm gọn trong cột điểm của đề (<c>numeric(6, 2)</c>).
    /// </summary>
    public const decimal MaxPointPerQuestion = 100m;

    /// <summary>
    /// Trần của tổng <c>số câu mỗi mức × điểm mức đó</c> — chính là <c>MaxScore</c> của mọi lượt làm
    /// đề này. Cột <c>ExamAttempt.Score</c>/<c>MaxScore</c> khai <c>numeric(6, 2)</c>, nên vượt mốc
    /// này là <c>PostgresException 22003</c> ngay lúc học sinh đầu tiên bắt đầu làm bài — quá muộn để
    /// giáo viên sửa. Bản nháp vẫn qua cửa này (nó chưa phải bằng 100) nhưng không được tràn cột.
    /// </summary>
    public const decimal MaxTotalScore = 9999.99m;

    /// <summary>Số chữ số thập phân tối đa của điểm một câu, khớp phần lẻ của <c>numeric(6, 2)</c>.</summary>
    public const int PointDecimalPlaces = 2;

    public SaveExamRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty().WithMessage("Tên bài kiểm tra không được để trống.")
            .MaximumLength(MaxTitleLength)
            .WithMessage($"Tên bài kiểm tra không được dài quá {MaxTitleLength} ký tự.");

        RuleFor(request => request.Description)
            .MaximumLength(MaxDescriptionLength)
            .WithMessage($"Mô tả không được dài quá {MaxDescriptionLength} ký tự.");

        RuleFor(request => request.Topic)
            .MaximumLength(MaxTopicLength)
            .WithMessage($"Tên chủ đề không được dài quá {MaxTopicLength} ký tự.");

        RuleFor(request => request.DurationMinutes)
            .InclusiveBetween(1, MaxDurationMinutes)
            .WithMessage($"Thời gian làm bài phải từ 1 đến {MaxDurationMinutes} phút.");

        // Đề theo danh sách câu hỏi không gửi các trường ma trận (server tự dẫn xuất từ danh sách), nên cả
        // nhóm quy tắc ma trận chỉ chạy khi request không mang `questions`.
        RuleFor(request => request.TotalQuestions)
            .InclusiveBetween(1, MaxTotalQuestions)
            .WithMessage($"Số câu hỏi của đề phải từ 1 đến {MaxTotalQuestions}.")
            .When(HasPaperSpecInput);

        // Chia đề lớn thành nhiều đề nhỏ: chỉ có nghĩa với đề soạn theo danh sách.
        RuleFor(request => request.PaperCount)
            .InclusiveBetween(1, PaperPlanner.MaxPaperCount)
            .WithMessage($"Số đề phải từ 1 đến {PaperPlanner.MaxPaperCount}.");

        RuleFor(request => request.PaperCount)
            .Equal(1)
            .When(request => request.Questions is null)
            .WithMessage("Chia thành nhiều đề chỉ dùng được với đề soạn theo danh sách câu hỏi.");

        RuleFor(request => request.PaperBonusUnits)
            .InclusiveBetween(0, MaxTotalQuestions)
            .WithMessage("Điểm cộng thêm không hợp lệ.");

        RuleFor(request => request.PaperBonusUnits)
            .Equal(0)
            .When(request => request.PaperCount <= 1)
            .WithMessage("Điểm cộng thêm chỉ dùng được khi chia thành nhiều đề.");

        AddPointRules(request => request.PointEasy, "Dễ", QuestionDifficulty.Easy);
        AddPointRules(request => request.PointMedium, "Trung bình", QuestionDifficulty.Medium);
        AddPointRules(request => request.PointHard, "Khó", QuestionDifficulty.Hard);

        // Tích của số câu và điểm vẫn có thể vượt numeric(6,2) dù từng trần đã qua — mốc thật, tính
        // trên chính công thức MaxScore. "Đúng bằng 100" là việc của service (nháp được miễn).
        RuleFor(request => request)
            .Must(FitsMaxTotalScore)
            .WithName(nameof(SaveExamRequest.PointEasy))
            .WithMessage(
                $"Tổng điểm bài kiểm tra không được vượt quá {MaxTotalScore}. "
                + "Hãy giảm số câu hoặc điểm từng mức.");

        // Ba tỷ lệ kiểm riêng từng cái ở đây; tổng của chúng chỉ bắt buộc bằng 100 khi đề không
        // phải bản nháp, nên phép kiểm đó thuộc về service.
        RuleFor(request => request.EasyPercentage)
            .InclusiveBetween(0, 100).WithMessage("Tỷ lệ câu Dễ phải nằm trong khoảng 0 đến 100.")
            .When(HasPaperSpecInput);

        RuleFor(request => request.MediumPercentage)
            .InclusiveBetween(0, 100).WithMessage("Tỷ lệ câu Trung bình phải nằm trong khoảng 0 đến 100.")
            .When(HasPaperSpecInput);

        RuleFor(request => request.HardPercentage)
            .InclusiveBetween(0, 100).WithMessage("Tỷ lệ câu Khó phải nằm trong khoảng 0 đến 100.")
            .When(HasPaperSpecInput);

        AddQuestionListRules();

        RuleFor(request => request.CloseAt)
            .GreaterThan(request => request.OpenAt)
            .WithMessage("Thời gian đóng bài phải sau thời gian mở bài.");

        // Phép kiểm frontend mới chỉ cảnh báo. Đặt sau quy tắc CloseAt > OpenAt nhưng FluentValidation
        // chạy độc lập từng rule, nên khi hai mốc ngược nhau người dùng nhận cả hai câu — vẫn đúng,
        // vì một cửa sổ âm thì đương nhiên không chứa nổi thời lượng nào.
        RuleFor(request => request)
            .Must(FitsInsideWindow)
            .WithName(nameof(SaveExamRequest.DurationMinutes))
            .WithMessage("Thời gian làm bài dài hơn khoảng thời gian mở đề, nên không học sinh nào làm hết bài được.");

        RuleFor(request => request.Lifecycle)
            .Must(BeDraftOrEmpty)
            .WithMessage("Trường 'lifecycle' chỉ nhận giá trị 'draft', hoặc bỏ trống để đề có hiệu lực theo lịch.");
    }

    /// <summary>
    /// Quy tắc cho điểm của một mức. Mức không có câu nào trong đề thì điểm của nó vô nghĩa, nên chỉ cần
    /// không âm; mức có câu thì phải lớn hơn 0.
    /// </summary>
    private void AddPointRules(
        System.Linq.Expressions.Expression<Func<SaveExamRequest, decimal>> selector,
        string levelLabel,
        QuestionDifficulty difficulty)
    {
        var read = selector.Compile();

        RuleFor(selector)
            .GreaterThanOrEqualTo(0m).WithMessage($"Điểm mỗi câu {levelLabel} không được âm.")
            .LessThanOrEqualTo(MaxPointPerQuestion)
            .WithMessage($"Điểm mỗi câu {levelLabel} không được vượt quá {MaxPointPerQuestion}.")
            .Must(HaveAtMostTwoDecimalPlaces)
            .WithMessage(
                $"Điểm mỗi câu {levelLabel} chỉ được tối đa {PointDecimalPlaces} chữ số thập phân, "
                + "vì cột điểm trong hệ thống lưu đúng chừng đó — phần lẻ dư ra sẽ bị làm tròn âm thầm "
                + "lúc lưu.")
            .When(IsMatrixRequest);

        // Chỉ khi ba tỷ lệ cộng đúng 100 thì số câu mỗi mức mới nói lên điều giáo viên muốn. Bản nháp có thể
        // mới nhập dở, và khi đó mức Khó nhận cả phần dư: 30/30/0 trên 10 câu cho 4 câu Khó dù giáo viên
        // nhập 0%. Không xuất bản được trong tình trạng đó (tổng tỷ lệ chặn), còn khi tỷ lệ đủ 100 thì
        // luật này chạy lại ở chính request xuất bản.
        RuleFor(selector)
            .GreaterThan(0m)
            .When(request => IsMatrixRequest(request)
                             && PercentagesAddUpToWhole(request)
                             && CountOf(request, difficulty) > 0)
            .WithMessage($"Đề có câu {levelLabel}, nên điểm mỗi câu {levelLabel} phải lớn hơn 0.");
    }

    private static bool IsMatrixRequest(SaveExamRequest request) => request.Questions is null;

    /// <summary>
    /// Số câu và tỷ lệ ba mức là đầu vào của giáo viên: đề theo ma trận, hoặc đề theo danh sách được chia thành nhiều
    /// đề (khi đó là cấu hình của một đề nhỏ). Đề theo danh sách một đề thì chúng là số liệu server tự dẫn xuất.
    /// </summary>
    private static bool HasPaperSpecInput(SaveExamRequest request) =>
        IsMatrixRequest(request) || request.PaperCount > 1;

    /// <summary>
    /// Quy tắc của danh sách câu hỏi: hình dạng thôi. Câu có thật không, có đang bật không là việc của
    /// service vì cần database.
    /// </summary>
    private void AddQuestionListRules()
    {
        RuleFor(request => request.Questions)
            .Must(list => list!.Count <= MaxTotalQuestions)
            .WithMessage($"Một đề có tối đa {MaxTotalQuestions} câu hỏi.")
            .When(request => request.Questions is not null);

        // Bộ kiểm con bỏ qua phần tử null, nên "questions": [null] phải bị chặn riêng ở đây; để lọt thì mọi nơi
        // đọc .QuestionId / .Point phía sau (kể cả ở service) ném NullReferenceException, tức 500 thay vì 400.
        RuleForEach(request => request.Questions!)
            .NotNull()
            .WithMessage("Danh sách câu hỏi chứa một mục rỗng.")
            .When(request => request.Questions is not null);

        RuleFor(request => request.Questions)
            .Must(list => list!
                .Where(item => item is not null)
                .Select(item => item.QuestionId)
                .Distinct()
                .Count() == list!.Count(item => item is not null))
            .WithMessage("Một câu hỏi chỉ được xuất hiện một lần trong đề.")
            .When(request => request.Questions is not null);

        RuleForEach(request => request.Questions!)
            .ChildRules(item =>
            {
                item.RuleFor(row => row.QuestionId)
                    .GreaterThan(0)
                    .WithMessage("Mã câu hỏi trong danh sách không hợp lệ.");

                item.RuleFor(row => row.Point)
                    .InclusiveBetween(0m, MaxPointPerQuestion)
                    .WithMessage($"Điểm của một câu phải nằm trong khoảng 0 đến {MaxPointPerQuestion}.")
                    .Must(HaveAtMostTwoDecimalPlaces)
                    .WithMessage(
                        $"Điểm của một câu chỉ được tối đa {PointDecimalPlaces} chữ số thập phân, vì cột điểm "
                        + "trong hệ thống lưu đúng chừng đó.");
            })
            .When(request => request.Questions is not null);
    }

    private static bool PercentagesAddUpToWhole(SaveExamRequest request) =>
        (long)request.EasyPercentage + request.MediumPercentage + request.HardPercentage == 100;

    private static int CountOf(SaveExamRequest request, QuestionDifficulty difficulty) =>
        BlueprintCalculator.CalculateRequiredCounts(
            request.TotalQuestions,
            request.EasyPercentage,
            request.MediumPercentage)[difficulty];

    private static bool FitsMaxTotalScore(SaveExamRequest request)
    {
        if (request.Questions is not null)
        {
            // Từng điểm đã có rule riêng; chỉ cộng khi cả danh sách nằm trong trần để phép cộng không tràn.
            // Phần tử null đã có rule riêng báo lỗi; không tính tổng trên danh sách còn mục rỗng.
            if (request.Questions.Any(item => item is null || !IsWithinPointRange(item.Point)))
            {
                return true;
            }

            return ExamScoring.TotalOf(request) <= MaxTotalScore;
        }

        // Điểm vượt trần từng câu đã có rule riêng báo lỗi. Không tính tổng trên con số đó: decimal có thể
        // tràn (điểm cỡ 7.9e28 nhân số câu) và ném OverflowException, tức 500 thay vì 400.
        if (!IsWithinPointRange(request.PointEasy)
            || !IsWithinPointRange(request.PointMedium)
            || !IsWithinPointRange(request.PointHard))
        {
            return true;
        }

        return ExamScoring.TotalOf(request) <= MaxTotalScore;
    }

    private static bool IsWithinPointRange(decimal point) => point >= 0m && point <= MaxPointPerQuestion;

    private static bool HaveAtMostTwoDecimalPlaces(decimal value) =>
        decimal.Round(value, PointDecimalPlaces) == value;

    private static bool FitsInsideWindow(SaveExamRequest request)
    {
        // Cửa sổ ngược đã có quy tắc riêng báo lỗi; ở đây bỏ qua để người dùng không nhận hai câu
        // nói cùng một chuyện.
        if (request.CloseAt <= request.OpenAt)
        {
            return true;
        }

        return request.DurationMinutes <= (request.CloseAt - request.OpenAt).TotalMinutes;
    }

    private static bool BeDraftOrEmpty(string? lifecycle) =>
        string.IsNullOrWhiteSpace(lifecycle)
        || string.Equals(
            lifecycle.Trim(),
            ExamMappingExtensions.DraftLifecycleValue,
            StringComparison.OrdinalIgnoreCase);
}
