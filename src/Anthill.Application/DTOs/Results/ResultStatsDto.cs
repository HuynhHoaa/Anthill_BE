namespace Anthill.Application.DTOs.Results;

/// <summary>Bốn ô số liệu đầu trang Kết quả của giáo viên.</summary>
/// <remarks>
/// Nhận <b>cùng bộ lọc</b> với bảng bên dưới. Bản đặc tả ghi nhận endpoint này ban đầu không nhận
/// bộ lọc, khiến bốn ô luôn là số toàn hệ thống trong khi bảng ngay dưới đã lọc — và frontend phải
/// dán thêm chữ "toàn hệ thống" để người dùng khỏi hiểu nhầm. Một màn hình mà hai phần nói về hai
/// tập dữ liệu khác nhau là một màn hình gây hiểu sai, nên ở đây chúng dùng chung bộ lọc.
/// </remarks>
public sealed record ResultStatsDto
{
    /// <summary>Số lượt làm bài khớp bộ lọc, ở mọi trạng thái.</summary>
    public required int TotalAttempts { get; init; }

    /// <summary>Số lượt đã chấm xong.</summary>
    public required int GradedCount { get; init; }

    /// <summary>
    /// Điểm trung bình trên các lượt <b>đã chấm</b>, làm tròn một chữ số (§5).
    /// </summary>
    /// <remarks>
    /// Chỉ tính trên lượt đã chấm chứ không tính trên toàn bộ: gộp cả bài đang làm dở vào mẫu số
    /// sẽ kéo trung bình xuống theo tiến độ chấm chứ không theo năng lực học sinh. NULL khi chưa
    /// có bài nào được chấm — trả 0 ở đó thì màn hình hiện "trung bình 0 điểm" cho một lớp chưa ai thi.
    /// </remarks>
    public decimal? AverageScore { get; init; }

    /// <summary>Điểm cao nhất trong các lượt đã chấm. NULL cùng lúc với <see cref="AverageScore"/>.</summary>
    public decimal? HighestScore { get; init; }

    /// <summary>Tỷ lệ phần trăm lượt đã nộp trên tổng số bài đã giao, làm tròn một chữ số.</summary>
    public required decimal CompletionRate { get; init; }
}
