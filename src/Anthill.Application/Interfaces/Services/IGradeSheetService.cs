using Anthill.Application.Common;
using Anthill.Application.DTOs.Files;

namespace Anthill.Application.Interfaces.Services;

/// <summary>
/// Xuất bảng điểm ra Excel. Hai người xuất được, mỗi người một phạm vi: <b>học sinh</b> xuất bảng điểm của chính mình, <b>giáo viên</b>
/// xuất bảng điểm của một lớp mình dạy. Quản trị viên không xuất ở đây.
/// </summary>
public interface IGradeSheetService
{
    /// <summary>
    /// Bảng điểm của học sinh đang đăng nhập: mọi bài đã nộp, mỗi bài một dòng.
    /// </summary>
    /// <remarks>
    /// Điểm đi qua đúng cổng của trang Kết quả: bài nào giáo viên chưa cho xem điểm (tắt <c>showResultAfterSubmit</c>) thì dòng đó ghi
    /// "Chưa công bố" và <b>không có</b> số nào trong file. Xuất file không phải đường vòng để đọc điểm sớm.
    /// <paramref name="termId"/> giống bảng điểm trên màn hình: bỏ trống là mọi kỳ, <c>0</c> là bài của lớp chưa phân kỳ, còn lại là
    /// mã kỳ (kỳ không có bài nào cho file chỉ có tiêu đề, không phải lỗi).
    /// </remarks>
    Task<Result<ExportedFileDto>> ExportMyResultsAsync(int? termId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bảng điểm của một lớp do giáo viên đang đăng nhập dạy: mỗi học sinh một dòng, mỗi đề đã giao cho lớp một cột, kèm điểm trung
    /// bình quy về thang 10.
    /// </summary>
    /// <remarks>
    /// Có <b>mọi</b> đề đã giao cho lớp, kể cả đề do giáo viên trước của lớp soạn: giáo viên phụ trách lớp cần nắm tình hình cả lớp.
    /// Lớp không thuộc giáo viên (hoặc còn là bản nháp) trả 404 như mọi nơi khác. Lớp đã khoá vẫn xuất được: khoá lớp chỉ đóng việc
    /// thay đổi, bảng điểm vẫn là bảng điểm.
    /// </remarks>
    Task<Result<ExportedFileDto>> ExportGroupAsync(int groupId, CancellationToken cancellationToken = default);
}
