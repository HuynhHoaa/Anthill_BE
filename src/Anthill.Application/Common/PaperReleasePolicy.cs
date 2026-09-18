namespace Anthill.Application.Common;

/// <summary>Việc phải làm khi giao THÊM người vào một đề dùng bản đề chung (H10).</summary>
public enum SharedPaperAssignmentDecision
{
    /// <summary>Dùng lại bản đề chung hiện có (hoặc sinh mới nếu chưa có) như bình thường.</summary>
    Reuse,

    /// <summary>Bản đề chung đã có người thấy đáp án — từ chối giao thêm người mới vào chính đề này.</summary>
    RejectAlreadyReleased
}

/// <summary>
/// Đề tắt <c>RandomizePerStudent</c> dùng đúng MỘT bản đề cho mọi lượt giao (§5). Bản đề đó là một bí
/// mật chung: một khi có em đã THẬT SỰ bắt đầu làm và cửa sổ hiệu lực của lượt đó đã đóng, em ấy xem
/// được đáp án đúng qua trang kết quả. Giao thêm người mới vào lúc này — một lớp khác, hay một em vào
/// muộn — mà vẫn dùng lại đúng bản đề ấy (như <c>ExamGeneratorService</c> từng luôn làm, không điều
/// kiện) là phát cho người mới chính bộ đề mà đáp án đã lộ ra ngoài.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cửa sổ đã đóng một mình không đủ để chặn — phải có em THẬT SỰ bắt đầu làm.</b> Một lượt giao đặt
/// khung giờ ở quá khứ mà chưa từng ai vào thi (giáo viên giao lớp buổi sáng với khung giờ đã trôi qua,
/// rồi giao tiếp lớp buổi chiều — cách dùng bình thường của per-class-schedule, không phải một vụ lộ
/// đề) không có gì để lộ: chưa ai từng đọc được một câu nào trong bản đề đó.
/// </para>
/// <para>
/// Không áp dụng khi bản đề chung đã bị xoá: sửa đề dưới ngưỡng "chưa ai bắt đầu" luôn xoá bản đề cũ
/// trước khi sinh lại (H9, xem <c>ExamEditPolicy.DecidePaperRefresh</c>), nên lúc đó
/// <paramref name="sharedPaperExists"/> — tham số của <see cref="DecideSharedPaperReuse"/> — là
/// <c>false</c> và hàm này luôn trả <see cref="SharedPaperAssignmentDecision.Reuse"/>: sửa đề không bao
/// giờ bị luật này chặn, chỉ đường GIAO BÀI mới có thể bị chặn.
/// </para>
/// <para>
/// Chặn thay vì âm thầm sinh một bản đề thứ hai cho người mới: <c>RandomizePerStudent = false</c> nghĩa
/// là "cả lớp một bộ câu" (lời hứa của chính cờ đó), và dữ liệu chỉ có chỗ cho một bản đề chung mỗi đề
/// (<c>ExamPaper.AssignmentId = NULL</c> là duy nhất theo <c>ExamId</c>). Hai bản đề chung khác nhau cho
/// cùng một đề phá vỡ đúng lời hứa đó, còn cách nói "hãy nhân bản đề" đã là lối ra quen thuộc mà
/// <c>ExamAssignmentService.AssignAsync</c> dùng cho ca tương tự (đổi cờ giữa chừng).
/// </para>
/// </remarks>
public static class PaperReleasePolicy
{
    /// <param name="randomizePerStudent">Cờ của đề. Bật thì mỗi em một bản đề riêng — không có gì để lộ chéo.</param>
    /// <param name="sharedPaperExists">Bản đề chung (<c>AssignmentId = NULL</c>) hiện đã tồn tại chưa.</param>
    /// <param name="hasAnyStartedAttempt">
    /// Trong những lượt giao ĐANG có (không tính lượt giao mới đang xét thêm), đã có em nào từng bắt đầu
    /// làm bài chưa — bất kể đang làm dở hay đã nộp. <c>false</c> thì không có gì để lộ, bất kể lịch nói
    /// gì: chưa ai từng mở bản đề ra xem.
    /// </param>
    /// <param name="priorReleaseAt">
    /// Mốc mở đáp án tính trên những lượt giao ĐANG có (không tính những lượt giao mới đang được xét
    /// thêm vào) — xem <c>IExamAssignmentRepository.GetAnswersReleaseAtAsync</c> bản có loại trừ.
    /// <c>NULL</c> khi đề chưa từng giao cho ai.
    /// </param>
    /// <param name="asOfUtc">Giờ server lúc quyết định.</param>
    public static SharedPaperAssignmentDecision DecideSharedPaperReuse(
        bool randomizePerStudent,
        bool sharedPaperExists,
        bool hasAnyStartedAttempt,
        DateTime? priorReleaseAt,
        DateTime asOfUtc)
    {
        if (randomizePerStudent || !sharedPaperExists || !hasAnyStartedAttempt)
        {
            return SharedPaperAssignmentDecision.Reuse;
        }

        return priorReleaseAt is { } releaseAt && releaseAt <= asOfUtc
            ? SharedPaperAssignmentDecision.RejectAlreadyReleased
            : SharedPaperAssignmentDecision.Reuse;
    }
}
