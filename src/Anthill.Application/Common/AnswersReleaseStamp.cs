namespace Anthill.Application.Common;

/// <summary>Kết luận của <see cref="AnswersReleaseStamp.Resolve"/>.</summary>
/// <param name="EffectiveReleaseAt">Mốc mở đáp án nên dùng NGAY cho lần đọc này.</param>
/// <param name="StampToPersist">
/// Khác NULL khi đây là lần đầu tiên mốc mở đáp án được ghi nhận là đã qua — người gọi phải lưu giá trị
/// này xuống <c>Exam.AnswersReleasedAt</c> trước khi trả kết quả. NULL nghĩa là không có gì mới để ghi
/// (hoặc đã có mốc cố định từ trước, hoặc mốc động vẫn còn ở tương lai).
/// </param>
public readonly record struct AnswersReleaseResolution(DateTime? EffectiveReleaseAt, DateTime? StampToPersist);

/// <summary>
/// H10 (nửa còn lại) — mốc mở đáp án phải "chỉ tiến, không lùi". <c>IExamAssignmentRepository.
/// GetAnswersReleaseAtAsync</c> tính lại <c>MAX</c> giờ đóng có hiệu lực trên MỌI lượt giao HIỆN CÓ mỗi
/// lần được gọi — đúng cho câu hỏi "sinh đề chung có an toàn để dùng lại không" (H10 nửa đầu, xem
/// <see cref="PaperReleasePolicy"/>), nhưng sai cho câu hỏi "em này đã từng thấy đáp án chưa": gán thêm
/// một lớp mới với giờ đóng muộn hơn — ví dụ một suất thi bù cho một em khác lớp — kéo MAX động đó ra xa
/// hơn, khiến những em ĐÃ hợp lệ xem đáp án tuần trước bỗng dưng bị giấu lại đáp án giữa chừng.
/// </summary>
/// <remarks>
/// <para>
/// Cách chữa: chụp lại mốc động ấy thành một giá trị CỐ ĐỊNH ngay lần đầu tiên nó được ghi nhận là đã
/// qua (<c>Exam.AnswersReleasedAt</c>), rồi từ đó luôn dùng giá trị cố định thay vì tính lại — bất kể
/// sau này có bao nhiêu lượt giao mới xuất hiện.
/// </para>
/// <para>
/// Hàm thuần, không đụng database: người gọi (<c>ResultService</c>) tự đọc cột đã lưu, tự tính mốc động
/// khi cần, đưa cả hai vào đây, rồi tự lưu <see cref="AnswersReleaseResolution.StampToPersist"/> nếu có.
/// </para>
/// </remarks>
public static class AnswersReleaseStamp
{
    /// <param name="storedReleasedAt"><c>Exam.AnswersReleasedAt</c> hiện tại. Khác NULL nghĩa là đã chốt từ trước.</param>
    /// <param name="dynamicReleaseAt">
    /// Mốc tính động ngay bây giờ (<c>GetAnswersReleaseAtAsync</c>) — chỉ cần khi <paramref name="storedReleasedAt"/>
    /// còn NULL. NULL nghĩa là đề chưa từng giao cho ai.
    /// </param>
    /// <param name="asOfUtc">Giờ server lúc đọc.</param>
    public static AnswersReleaseResolution Resolve(DateTime? storedReleasedAt, DateTime? dynamicReleaseAt, DateTime asOfUtc)
    {
        // Đã chốt từ một lần đọc trước — dùng lại, không tính động nữa dù lịch hiện tại nói gì khác.
        if (storedReleasedAt is { } stored)
        {
            return new AnswersReleaseResolution(stored, StampToPersist: null);
        }

        // Lần đầu tiên mốc động cho thấy đã qua giờ mở đáp án: chốt lại ngay, để lần đọc sau (dù của em
        // nào, dù lúc đó có thêm bao nhiêu lượt giao mới) không còn phụ thuộc MAX động nữa.
        if (dynamicReleaseAt is { } dynamicValue && dynamicValue <= asOfUtc)
        {
            return new AnswersReleaseResolution(dynamicValue, StampToPersist: dynamicValue);
        }

        // Chưa tới giờ (hoặc chưa từng giao cho ai) — trả mốc động để màn hình còn hẹn được "mở lúc mấy
        // giờ", nhưng không có gì để chốt.
        return new AnswersReleaseResolution(dynamicReleaseAt, StampToPersist: null);
    }
}
