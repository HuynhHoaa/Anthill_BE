using Anthill.Application.Common;
using Anthill.Application.DTOs.Accounts;
using Anthill.Application.DTOs.Teachers;
using Anthill.Domain.Enums;

namespace Anthill.Application.Interfaces.Services;

/// <summary>
/// Quản lý tài khoản giáo viên — chỉ quản trị viên. Nhà trường cấp tài khoản, gán môn, khoá khi giáo viên nghỉ; giáo viên
/// không tự đổi email, số điện thoại hay môn của mình.
/// </summary>
/// <remarks>
/// Không có xoá: giáo viên sở hữu câu hỏi, đề và lịch sử giao bài, xoá dòng là làm thủng chúng. Giáo viên nghỉ việc thì
/// <b>khoá</b> tài khoản (kèm lý do), rồi đổi giáo viên phụ trách của các lớp họ đang dạy.
/// </remarks>
public interface ITeacherService
{
    Task<Result<PagedResult<TeacherDto>>> ListAsync(ListTeachersQuery query, CancellationToken cancellationToken = default);

    /// <summary>Một giáo viên kèm các lớp đang phụ trách.</summary>
    Task<Result<TeacherDto>> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<TeacherDto>> CreateAsync(CreateTeacherRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sửa họ tên, email, số điện thoại, môn. Đổi môn bị từ chối (409) khi giáo viên đã soạn câu hỏi hay đề: ngân hàng và
    /// việc sinh đề đều chia theo môn của người soạn, đổi môn giữa chừng làm chúng lệch nhau.
    /// </summary>
    Task<Result<TeacherDto>> UpdateAsync(int id, UpdateTeacherRequest request, CancellationToken cancellationToken = default);

    /// <summary>Khoá (lý do bắt buộc, xoay <c>SecurityStamp</c>, cắt kết nối đang mở) hoặc mở khoá.</summary>
    Task<Result<TeacherDto>> SetStatusAsync(
        int id,
        UserStatus status,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>Cấp mật khẩu ngẫu nhiên mới, ép đổi ở lần đăng nhập đầu, đăng xuất mọi phiên đang mở.</summary>
    Task<Result<TeacherPasswordResponse>> ResetPasswordAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lịch sử khoá / mở khoá kèm lý do, mới nhất trước.</summary>
    Task<Result<IReadOnlyList<AccountChangeDto>>> ListChangesAsync(int id, CancellationToken cancellationToken = default);
}
