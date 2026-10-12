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
    /// Điểm một câu không bao giờ lớn hơn tổng điểm của cả đề. Cũng nằm gọn trong cột điểm của đề
    /// (<c>numeric(6, 2)</c>).
    /// </summary>
    public const decimal MaxPointPerQuestion = ExamScoring.TotalScore;

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

        RuleFor(request => request.TotalQuestions)
            .InclusiveBetween(1, MaxTotalQuestions)
            .WithMessage($"Số câu hỏi của đề phải từ 1 đến {MaxTotalQuestions}.");

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
            .InclusiveBetween(0, 100).WithMessage("Tỷ lệ câu Dễ phải nằm trong khoảng 0 đến 100.");

        RuleFor(request => request.MediumPercentage)
            .InclusiveBetween(0, 100).WithMessage("Tỷ lệ câu Trung bình phải nằm trong khoảng 0 đến 100.");

        RuleFor(request => request.HardPercentage)
            .InclusiveBetween(0, 100).WithMessage("Tỷ lệ câu Khó phải nằm trong khoảng 0 đến 100.");

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
                + "lúc lưu.");

        RuleFor(selector)
            .GreaterThan(0m)
            .When(request => CountOf(request, difficulty) > 0)
            .WithMessage($"Đề có câu {levelLabel}, nên điểm mỗi câu {levelLabel} phải lớn hơn 0.");
    }

    private static int CountOf(SaveExamRequest request, QuestionDifficulty difficulty) =>
        BlueprintCalculator.CalculateRequiredCounts(
            request.TotalQuestions,
            request.EasyPercentage,
            request.MediumPercentage)[difficulty];

    private static bool FitsMaxTotalScore(SaveExamRequest request) =>
        ExamScoring.TotalOf(request) <= MaxTotalScore;

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
