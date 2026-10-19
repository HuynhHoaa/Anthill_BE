using Anthill.Application.Common;
using Anthill.Application.DTOs.Imports;
using Anthill.Application.Interfaces.Files;

namespace Anthill.Application.Interfaces.Services;

/// <summary>
/// Nhập học sinh từ file Excel — chỉ quản trị viên. Mỗi kỳ có vài nghìn học sinh, không ai nhập tay từng em.
/// </summary>
/// <remarks>
/// Dòng hỏng không làm hỏng cả mẻ: dòng hợp lệ được nhập, dòng hỏng được liệt kê kèm số dòng và lý do. Nhập hai bước (kiểm tra
/// thử rồi nhập thật) chạy cùng một đường mã, nên bản xem trước không hứa điều bản nhập thật không làm.
/// </remarks>
public interface IStudentImportService
{
    /// <summary>File mẫu .xlsx: trang nhập liệu (có ô chọn lớp) và trang hướng dẫn.</summary>
    Task<Result<byte[]>> BuildTemplateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo tài khoản cho học sinh chưa có và xếp lớp; học sinh đã có (theo mã) chỉ được xếp lớp nếu chưa có lớp — hồ sơ không
    /// bao giờ bị ghi đè từ file.
    /// </summary>
    Task<Result<ImportResultDto>> ImportAsync(
        Stream file,
        ImportStudentsOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>Cùng luật với <see cref="ImportAsync"/> nhưng nhận sẵn các dòng đã đọc (nhập tổng hợp dựng dòng từ một file duy nhất).</summary>
    Task<Result<ImportResultDto>> ImportRowsAsync(
        IReadOnlyList<SpreadsheetRow> rows,
        ImportStudentsOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>Nhập giáo viên từ file Excel — chỉ quản trị viên.</summary>
public interface ITeacherImportService
{
    Task<Result<byte[]>> BuildTemplateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo tài khoản giáo viên (email là tên đăng nhập) kèm môn giảng dạy. Email đã có người dùng thì báo lỗi dòng đó, trừ khi
    /// đó chính là giáo viên cùng tên — khi ấy dòng được tính là đã nhập rồi.
    /// </summary>
    Task<Result<ImportResultDto>> ImportAsync(
        Stream file,
        ImportTeachersOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>Cùng luật với <see cref="ImportAsync"/> nhưng nhận sẵn các dòng đã đọc.</summary>
    Task<Result<ImportResultDto>> ImportRowsAsync(
        IReadOnlyList<SpreadsheetRow> rows,
        ImportTeachersOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>Nhập lớp từ file Excel — chỉ quản trị viên. Mỗi lớp có đúng một giáo viên phụ trách, ghi ngay trên dòng.</summary>
public interface IGroupImportService
{
    Task<Result<byte[]>> BuildTemplateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lập các lớp mới (bản nháp) kèm giáo viên phụ trách. Tên lớp đã có thì không tạo lại; đổi giáo viên của lớp đã có làm ở
    /// trang lớp và phải ghi lý do.
    /// </summary>
    Task<Result<ImportResultDto>> ImportAsync(
        Stream file,
        ImportGroupsOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>Cùng luật với <see cref="ImportAsync"/> nhưng nhận sẵn các dòng đã đọc.</summary>
    Task<Result<ImportResultDto>> ImportRowsAsync(
        IReadOnlyList<SpreadsheetRow> rows,
        ImportGroupsOptions options,
        CancellationToken cancellationToken = default);
}
