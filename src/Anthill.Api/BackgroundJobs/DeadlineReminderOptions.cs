namespace Anthill.Api.BackgroundJobs;

/// <summary>Cấu hình cho tiến trình nền nhắc học sinh sắp hết giờ làm bài.</summary>
public sealed class DeadlineReminderOptions
{
    public const string SectionName = "DeadlineReminder";

    /// <summary>Khoảng cách giữa hai lượt quét, tính bằng giây.</summary>
    /// <remarks>
    /// Quét thưa hơn thì lời nhắc tới muộn hơn đúng bấy nhiêu, mà con số này lại là thứ người dùng cảm nhận
    /// được: nhắc trước giờ đóng ba mươi phút mà lệch năm phút thì không sao, lệch nửa tiếng thì lời nhắc
    /// thành vô nghĩa. Ba mươi giây là nhịp của tiến trình tự nộp bài, và một truy vấn có chỉ mục mỗi nửa
    /// phút không đáng kể với một dịch vụ miễn phí.
    /// </remarks>
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>Số em nhắc tối đa trong một lượt quét.</summary>
    /// <remarks>
    /// Có trần để một đề giao cho cả trường không biến một lượt quét thành hàng nghìn transaction liên
    /// tiếp. Phần dư được nhắc ở vòng sau, chậm hơn nửa phút.
    /// </remarks>
    public int BatchSize { get; set; } = 200;

    /// <summary>Biên cộng vào thời lượng làm bài để ra mốc nhắc, tính bằng phút.</summary>
    /// <remarks>
    /// Mốc nhắc là "giờ đóng trừ thời lượng làm bài trừ biên này" — tức đúng lúc em còn vừa đủ giờ để làm
    /// trọn bài cộng thêm nửa tiếng xoay xở. Đặt về 0 thì lời nhắc tới đúng lúc em vừa kịp bắt đầu, không
    /// còn phút nào dự phòng.
    /// </remarks>
    public int MarginMinutes { get; set; } = 30;

    /// <summary>Bật hoặc tắt hẳn tiến trình nền.</summary>
    /// <remarks>Tắt được để chạy kiểm thử mà không có thông báo nào tự xuất hiện giữa chừng.</remarks>
    public bool Enabled { get; set; } = true;
}
