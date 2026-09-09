using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.Results;

namespace EduQuiz.Application.Interfaces.Services;

/// <summary>Kết quả làm bài — §4.8. Dùng chung cho màn hình giáo viên và học sinh.</summary>
/// <remarks>
/// Ràng buộc đã cài:
/// <list type="bullet">
/// <item><b>Quyền xem đáp án thực thi ở server (§3 ⑥).</b> Giáo viên sở hữu đề luôn xem được.
/// Học sinh chỉ xem khi bài là của mình <b>và</b> đã chấm xong <b>và</b> đề bật
/// <c>showResultAfterSubmit</c>. Không đủ điều kiện thì response <b>không chứa</b>
/// <c>correctAnswerId</c> hay <c>isCorrect</c> — ẩn ở giao diện là không đủ.</item>
/// <item><see cref="GetSummaryAsync"/> phục vụ màn hình "Đã nộp bài", nơi chỉ cần một vòng tròn
/// điểm. Trang đó vốn gọi endpoint chi tiết, tức kéo về toàn bộ câu hỏi để hiện một con số.</item>
/// <item><see cref="GetStatsAsync"/> nhận <b>cùng bộ lọc</b> với <see cref="ListAsync"/>: hai phần
/// nằm trên một màn hình thì phải nói về cùng một tập dữ liệu.</item>
/// <item>Học sinh gọi <see cref="GetDetailAsync"/> trên bài không phải của mình nhận
/// <see cref="ErrorCode.NotFound"/>, không phải Forbidden (§4.2).</item>
/// </list>
/// </remarks>
public interface IResultService
{
    /// <summary>Bảng kết quả của giáo viên, lọc chéo theo đề / học sinh / nhóm / trạng thái.</summary>
    Task<Result<PagedResult<ResultSummaryDto>>> ListAsync(
        ListResultsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Bốn ô số liệu đầu trang, tính trên đúng tập dữ liệu mà bảng đang hiển thị.</summary>
    Task<Result<ResultStatsDto>> GetStatsAsync(
        ListResultsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Chi tiết một lượt làm bài, soi từng câu.</summary>
    Task<Result<AttemptDetailDto>> GetDetailAsync(int attemptId, CancellationToken cancellationToken = default);

    /// <summary>Tóm tắt một lượt, cho màn hình vừa nộp xong.</summary>
    Task<Result<ResultSummaryDto>> GetSummaryAsync(int attemptId, CancellationToken cancellationToken = default);

    /// <summary>Lịch sử làm bài của chính học sinh đang đăng nhập.</summary>
    Task<Result<PagedResult<ResultSummaryDto>>> ListMyResultsAsync(
        ListMyResultsQuery query,
        CancellationToken cancellationToken = default);
}
