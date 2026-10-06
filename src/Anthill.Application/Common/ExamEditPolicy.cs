using Anthill.Application.DTOs.Exams;
using Anthill.Application.Mappings;
using Anthill.Domain.Entities;

namespace Anthill.Application.Common;

/// <summary>Việc phải làm khi giáo viên gửi <c>PUT /api/exams/{id}</c>.</summary>
public enum ExamEditDecision
{
    /// <summary>Ghi thẳng. Không bản đề nào đã sinh bị ảnh hưởng.</summary>
    Apply,

    /// <summary>Ghi, rồi sinh lại bản đề của mọi lượt giao hiện có — chưa em nào cầm bản cũ trên tay.</summary>
    ApplyAndRegenerate,

    /// <summary>
    /// Ghi, rồi bỏ mọi bản đề còn sót lại. Đề không còn lượt giao nào nên không có gì để sinh lại ngay:
    /// cấu hình mới áp ở lần giao sau.
    /// </summary>
    /// <remarks>
    /// Bản đề chung không gắn với lượt giao nào nên thu hồi hết các em không xoá nó. Để lại thì lần giao sau
    /// tìm thấy nó và phát lại bộ câu của cấu hình cũ, trong khi màn hình giáo viên hiện cấu hình mới.
    /// </remarks>
    ApplyAndDiscardPapers,

    /// <summary>Đã có em bắt đầu làm mà request đổi cấu hình đề — từ chối, không ghi gì.</summary>
    RejectFrozen,

    /// <summary>Đã có em bắt đầu làm mà request đưa đề về bản nháp — từ chối, không ghi gì.</summary>
    RejectBackToDraft
}

/// <summary>Kết luận của <see cref="ExamEditPolicy.Decide"/>.</summary>
/// <param name="Decision">Việc phải làm.</param>
/// <param name="FrozenFieldsChanged">
/// Nhãn tiếng Việt của những trường bị khoá mà request đang cố đổi, theo thứ tự hiện trên form. Rỗng
/// trừ khi <paramref name="Decision"/> là <see cref="ExamEditDecision.RejectFrozen"/>.
/// </param>
public sealed record ExamEditPlan(ExamEditDecision Decision, IReadOnlyList<string> FrozenFieldsChanged);

/// <summary>
/// Một chỗ duy nhất trả lời "sửa đề này lúc này thì được sửa những gì, và bản đề đã sinh có phải sinh lại
/// không".
/// </summary>
/// <remarks>
/// <para>
/// Ranh giới là <b>lượt làm bài đầu tiên</b>, không phải lúc giao bài hay lúc xuất bản. Trước mốc đó bản
/// đề chỉ là thứ nằm trong database: sinh lại thì chưa ai mất gì, nên giáo viên được sửa mọi thứ và học
/// sinh nhận bản đề khớp cấu hình mới. Sau mốc đó đã có em làm trên một bộ câu cụ thể. Đổi cấu hình lúc
/// này thì hoặc chỉ những em vào sau nhận đề mới — cả lớp làm hai đề khác nhau mà điểm vẫn đem so với
/// nhau — hoặc phải sinh lại cả bản đề em đang làm dở. Cả hai đều tệ hơn việc bảo giáo viên nhân bản đề
/// ra một đề mới.
/// </para>
/// <para>
/// Tên, mô tả và cờ hiện điểm vẫn sửa được sau mốc đó: chúng không đổi bộ câu nào, còn điểm đã
/// chấm thì luôn tính trên bản chụp của bản đề nên không trường nào ở đây chạm được tới. Lịch không nằm
/// trong chính sách này mà có luật khoá riêng theo giờ (<see cref="AssignmentSchedule.IsScheduleLocked(DateTime, DateTime, DateTime)"/>).
/// </para>
/// <para>
/// Là hàm thuần nên kiểm được toàn bộ ma trận bằng unit test, không cần database.
/// </para>
/// </remarks>
public static class ExamEditPolicy
{
    /// <param name="exam">Đề như đang nằm trong database, <b>trước</b> khi áp request.</param>
    /// <param name="request">Cấu hình mới giáo viên gửi lên.</param>
    /// <param name="hasAssignments">
    /// Đề đang có ít nhất một lượt giao — tức là có bản đề đang chờ các em. Đề từng giao rồi thu hồi hết
    /// thì là <c>false</c>, dù bản đề chung của lần giao cũ có thể vẫn còn.
    /// </param>
    /// <param name="hasStartedAttempts">Đã có ít nhất một em bắt đầu làm bài.</param>
    public static ExamEditPlan Decide(
        Exam exam,
        SaveExamRequest request,
        bool hasAssignments,
        bool hasStartedAttempts)
    {
        ArgumentNullException.ThrowIfNull(exam);
        ArgumentNullException.ThrowIfNull(request);

        if (hasStartedAttempts)
        {
            // Rút về nháp là cách chặn mọi em còn lại khỏi bài, kể cả em đang làm dở: lượt đang chạy vẫn
            // nộp được, nhưng những em chưa vào thì thấy "chưa mở" cho một đề mà bạn cùng lớp đã làm xong.
            if (IsDraftRequest(request) && !exam.IsDraft)
            {
                return new ExamEditPlan(ExamEditDecision.RejectBackToDraft, []);
            }

            var frozen = ChangedFrozenFields(exam, request);

            return frozen.Count > 0
                ? new ExamEditPlan(ExamEditDecision.RejectFrozen, frozen)
                : new ExamEditPlan(ExamEditDecision.Apply, []);
        }

        if (!ChangesGeneratedPaper(exam, request))
        {
            return new ExamEditPlan(ExamEditDecision.Apply, []);
        }

        // Không còn lượt giao nào thì không có ai để sinh lại cho, nhưng vẫn phải bỏ bản đề cũ: đề từng giao
        // rồi thu hồi hết vẫn giữ bản đề chung, và lần giao sau sẽ dùng lại nó thay vì sinh theo cấu hình mới.
        return hasAssignments
            ? new ExamEditPlan(ExamEditDecision.ApplyAndRegenerate, [])
            : new ExamEditPlan(ExamEditDecision.ApplyAndDiscardPapers, []);
    }

    /// <summary>Những trường bị khoá mà request đang cố đổi, theo thứ tự hiện trên form.</summary>
    /// <remarks>
    /// Rộng hơn <see cref="ChangesGeneratedPaper"/> đúng một trường: thời lượng. Bộ sinh đề không đọc nó,
    /// nhưng em đã vào thi có hạn nộp chốt theo thời lượng cũ, còn em vào sau sẽ nhận hạn theo thời lượng
    /// mới — cùng một đề mà hai em có hai quỹ thời gian khác nhau.
    ///
    /// <c>randomizePerStudent</c> không nằm trong request này nên không có mặt ở đây; cờ đó đã bị khoá từ
    /// lúc giao bài, ở endpoint giao bài.
    /// </remarks>
    public static IReadOnlyList<string> ChangedFrozenFields(Exam exam, SaveExamRequest request)
    {
        ArgumentNullException.ThrowIfNull(exam);
        ArgumentNullException.ThrowIfNull(request);

        var changed = new List<string>();

        if (exam.TotalQuestions != request.TotalQuestions)
        {
            changed.Add("số câu hỏi");
        }

        if (exam.DurationMinutes != request.DurationMinutes)
        {
            changed.Add("thời gian làm bài");
        }

        if (exam.PointPerQuestion != request.PointPerQuestion)
        {
            changed.Add("điểm mỗi câu");
        }

        if (!SameTopic(exam, request))
        {
            changed.Add("chủ đề");
        }

        if (exam.EasyPercentage != request.EasyPercentage)
        {
            changed.Add("tỷ lệ câu Dễ");
        }

        if (exam.MediumPercentage != request.MediumPercentage)
        {
            changed.Add("tỷ lệ câu Trung bình");
        }

        if (exam.HardPercentage != request.HardPercentage)
        {
            changed.Add("tỷ lệ câu Khó");
        }

        if (exam.ShuffleQuestions != request.ShuffleQuestions)
        {
            changed.Add("trộn thứ tự câu hỏi");
        }

        if (exam.ShuffleAnswers != request.ShuffleAnswers)
        {
            changed.Add("trộn thứ tự đáp án");
        }

        return changed;
    }

    /// <summary>
    /// Request có đổi thứ gì mà <c>ExamGeneratorService</c> đọc khi dựng bản đề không.
    /// </summary>
    /// <remarks>
    /// Danh sách này phải khớp từng trường với những gì bộ sinh đề đọc: bỏ sót một trường thì học sinh
    /// cầm bản đề của cấu hình cũ; thừa một trường thì bản đề bị sinh lại vô cớ, mà mỗi lần sinh lại là
    /// một lần bốc câu mới. Thời lượng, lịch, tên và cờ hiện điểm không có mặt vì bộ sinh đề không đọc
    /// chúng — màn hình làm bài đọc thẳng từ đề.
    ///
    /// Tỷ lệ Khó có mặt dù bộ sinh đề chỉ đọc Dễ và Trung bình (Khó nhận phần dư): đổi riêng nó mà giữ
    /// hai tỷ lệ kia là một cấu hình có tổng khác 100, và phép kiểm blueprint phía sau sẽ chặn nó.
    /// </remarks>
    public static bool ChangesGeneratedPaper(Exam exam, SaveExamRequest request)
    {
        ArgumentNullException.ThrowIfNull(exam);
        ArgumentNullException.ThrowIfNull(request);

        return exam.TotalQuestions != request.TotalQuestions
               || exam.EasyPercentage != request.EasyPercentage
               || exam.MediumPercentage != request.MediumPercentage
               || exam.HardPercentage != request.HardPercentage
               || exam.PointPerQuestion != request.PointPerQuestion
               || !SameTopic(exam, request)
               || exam.ShuffleQuestions != request.ShuffleQuestions
               || exam.ShuffleAnswers != request.ShuffleAnswers;
    }

    /// <summary>So chủ đề sau khi quy chuẩn cả hai vế: <c>""</c>, <c>"all"</c> và NULL là cùng một ý.</summary>
    private static bool SameTopic(Exam exam, SaveExamRequest request) =>
        string.Equals(
            ExamMappingExtensions.NormalizeTopic(exam.Topic),
            ExamMappingExtensions.NormalizeTopic(request.Topic),
            StringComparison.Ordinal);

    private static bool IsDraftRequest(SaveExamRequest request) =>
        string.Equals(
            request.Lifecycle?.Trim(),
            ExamMappingExtensions.DraftLifecycleValue,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Việc phải làm với bản đề đã sinh của MỘT đề khi nội dung ngân hàng câu hỏi đổi (H9).</summary>
    public enum PaperRefreshDecision
    {
        /// <summary>Sinh lại bản đề cho mọi lượt giao hiện có — chưa em nào cầm bản đề trên tay.</summary>
        Regenerate,

        /// <summary>Không còn lượt giao nào — bỏ bản đề cũ, không có gì để sinh lại ngay.</summary>
        DiscardPapers,

        /// <summary>Đã có em bắt đầu làm — giữ nguyên bản đề đang dùng, không đụng tới.</summary>
        Skip
    }

    /// <summary>
    /// Quyết định cho bản đề của MỘT đề khi một câu hỏi trong ngân hàng vừa được sửa nội dung hoặc bị ẩn
    /// (H9, gọi từ <c>QuestionService</c>). Cùng ranh giới "lượt làm bài đầu tiên" mà <see cref="Decide"/>
    /// dùng cho lần sửa cả đề — khác đúng một chỗ: <see cref="Decide"/> còn phải hỏi "request này có đổi
    /// gì mà bộ sinh đề đọc không" (<see cref="ChangesGeneratedPaper"/>); ở đây câu trả lời luôn là có,
    /// vì người gọi chỉ liệt kê những đề mà bản đề <b>hiện tại</b> đã chứa chính câu hỏi vừa đổi.
    /// </summary>
    /// <param name="hasAssignments">Đề đang có ít nhất một lượt giao.</param>
    /// <param name="hasStartedAttempts">Đã có ít nhất một em bắt đầu làm bài.</param>
    public static PaperRefreshDecision DecidePaperRefresh(bool hasAssignments, bool hasStartedAttempts)
    {
        if (hasStartedAttempts)
        {
            return PaperRefreshDecision.Skip;
        }

        return hasAssignments ? PaperRefreshDecision.Regenerate : PaperRefreshDecision.DiscardPapers;
    }
}
