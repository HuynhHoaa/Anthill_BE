using Anthill.Application.Common;
using Anthill.Application.DTOs.Imports;

namespace Anthill.Application.Interfaces.Services;

/// <summary>
/// Nhập tổng hợp từ <b>một</b> file Excel — chỉ quản trị viên. Trường thường giữ một danh sách duy nhất: mỗi dòng một học sinh, kèm
/// lớp và giáo viên chủ nhiệm của lớp đó. Một lần tải lên tạo giáo viên, lớp (kèm chủ nhiệm) và học sinh, xếp học sinh vào lớp; sau
/// đó mỗi thứ được quản lý ở trang riêng của nó.
/// </summary>
/// <remarks>
/// Dùng đúng các luật của ba bộ nhập riêng (giáo viên, lớp, học sinh), chạy nối tiếp trong <b>một transaction</b>: nhập thử chạy
/// toàn bộ rồi rollback, nên bản xem trước thấy được cả lớp và giáo viên mới tạo cùng lúc; nhập thật thì commit. Dòng hỏng không
/// làm hỏng cả mẻ; lỗi của giáo viên hay lớp kéo theo các học sinh của lớp đó chưa được xếp.
/// </remarks>
public interface ISchoolImportService
{
    /// <summary>File mẫu .xlsx: một trang nhập liệu (có ô chọn môn) và trang hướng dẫn.</summary>
    Task<Result<byte[]>> BuildTemplateAsync(CancellationToken cancellationToken = default);

    Task<Result<SchoolImportResultDto>> ImportAsync(
        Stream file,
        ImportSchoolOptions options,
        CancellationToken cancellationToken = default);
}
