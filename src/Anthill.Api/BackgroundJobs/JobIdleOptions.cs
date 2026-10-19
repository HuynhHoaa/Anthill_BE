namespace Anthill.Api.BackgroundJobs;

/// <summary>Cấu hình việc nghỉ quét của các tiến trình nền khi không có ai dùng hệ thống.</summary>
public sealed class JobIdleOptions
{
    public const string SectionName = "BackgroundJobs";

    /// <summary>
    /// Sau chừng này giây không có yêu cầu đã đăng nhập nào, tiến trình tự nộp bài và tiến trình nhắc sắp hết giờ
    /// ngừng quét database. Có người dùng trở lại thì chúng quét ngay.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lý do tồn tại: database miễn phí (Neon) chỉ ngủ khi 5 phút liền không có truy vấn nào, và gói miễn phí tính theo
    /// giờ database thức (100 CU-giờ mỗi tháng, cỡ 400 giờ). Hai tiến trình quét mỗi 30 giây khiến database không bao
    /// giờ ngủ: hết hạn mức sau khoảng 17 ngày, từ đó cả hệ thống không đăng nhập được tới đầu tháng sau.
    /// </para>
    /// <para>
    /// Mười phút là gấp đôi khoảng cách đều đặn dài nhất mà frontend tự gọi (trang làm bài đối chiếu giờ với server mỗi
    /// 5 phút), nên một học sinh ngồi yên trên một câu vẫn được tính là đang dùng. Cộng 5 phút chờ ngủ của chính
    /// database, một lượt dùng làm database thức thêm khoảng 15 phút sau thao tác cuối.
    /// </para>
    /// <para>
    /// Cái giá: lúc không ai dùng, bài bỏ dở không được tự nộp và lời nhắc chưa được tạo, cho tới khi có người dùng
    /// trở lại (quét ngay trong vòng vài giây). Cả hai chỉ có người nhìn thấy khi đang dùng hệ thống, nên khác biệt
    /// duy nhất là một trang theo dõi trực tiếp mà không ai bấm gì quá khoảng này sẽ không tự cập nhật.
    /// </para>
    /// <para>
    /// Đặt về 0 để không bao giờ nghỉ: hai tiến trình quét đều đặn như trước. Dùng khi database không tính theo giờ
    /// thức.
    /// </para>
    /// </remarks>
    public int IdleAfterSeconds { get; set; } = 600;
}
