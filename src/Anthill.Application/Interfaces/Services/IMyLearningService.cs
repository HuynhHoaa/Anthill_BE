using Anthill.Application.Common;
using Anthill.Application.DTOs.MyLearning;

namespace Anthill.Application.Interfaces.Services;

/// <summary>
/// Việc học của chính học sinh đang đăng nhập: các lớp em học và bảng điểm theo môn. Danh tính luôn lấy từ token, không có id học sinh
/// nào trên đường dẫn (§3 quy tắc ④).
/// </summary>
public interface IMyLearningService
{
    /// <summary>Các lớp đã công bố em đang học (kể cả lớp đã khoá), kèm số bài theo trạng thái.</summary>
    Task<Result<IReadOnlyList<MyGroupDto>>> ListMyGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>Một lớp của em; 404 khi em không học lớp đó (cũng là câu trả lời cho lớp không tồn tại hay còn là bản nháp).</summary>
    Task<Result<MyGroupDto>> GetMyGroupAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>Các kỳ em có lớp (để lọc danh sách lớp và bảng điểm).</summary>
    Task<Result<IReadOnlyList<MyTermDto>>> ListMyTermsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bảng điểm: các môn em có bài kèm điểm trung bình từng môn. <paramref name="termId"/>: bỏ trống là mọi kỳ; <c>0</c> là bài của
    /// lớp chưa phân kỳ (và bài giao lẻ); số khác là bài của lớp thuộc kỳ đó.
    /// </summary>
    Task<Result<TranscriptDto>> GetMyTranscriptAsync(int? termId = null, CancellationToken cancellationToken = default);
}
