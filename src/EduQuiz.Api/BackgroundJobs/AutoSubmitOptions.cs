namespace EduQuiz.Api.BackgroundJobs;

/// <summary>Cấu hình cho tiến trình nền tự nộp bài hết giờ.</summary>
public sealed class AutoSubmitOptions
{
    public const string SectionName = "AutoSubmit";

    /// <summary>
    /// Khoảng cách giữa hai lần quét, tính bằng giây.
    /// </summary>
    /// <remarks>
    /// Ba mươi giây là chỗ cân bằng: học sinh hết giờ chờ tối đa nửa phút mới thấy bài được nộp —
    /// đủ nhanh để không ai kịp lo — trong khi tiến trình chỉ chạy hai lần mỗi phút, không đáng kể
    /// với một dịch vụ miễn phí.
    ///
    /// Con số này <b>không</b> quyết định tính đúng đắn: hạn giờ nằm ở <c>ExpiresAt</c> trong
    /// database, nên quét muộn thì bài vẫn được chấm theo đúng mốc hết giờ chứ không theo lúc quét.
    /// </remarks>
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>Số bài xử lý tối đa mỗi lần quét.</summary>
    /// <remarks>
    /// Có trần để một đợt thi cả trường kết thúc cùng lúc không biến thành một transaction khổng
    /// lồ. Phần dư được xử lý ở vòng kế tiếp — và vì mỗi bài chấm theo <c>ExpiresAt</c> của chính
    /// nó, chờ thêm một vòng không làm sai điểm của ai.
    /// </remarks>
    public int BatchSize { get; set; } = 50;

    /// <summary>Bật hoặc tắt hẳn tiến trình nền.</summary>
    /// <remarks>Tắt được để chạy kiểm thử tích hợp mà không có ai tự nộp bài giữa chừng.</remarks>
    public bool Enabled { get; set; } = true;
}
