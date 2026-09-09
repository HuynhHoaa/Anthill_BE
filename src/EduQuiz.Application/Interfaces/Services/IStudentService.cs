using EduQuiz.Application.Common;
using EduQuiz.Application.DTOs.Students;
using EduQuiz.Domain.Enums;

namespace EduQuiz.Application.Interfaces.Services;

/// <summary>
/// Tài khoản học sinh — §4.3. Quản trị viên sở hữu tài khoản (tạo, sửa, khoá, reset mật khẩu),
/// giáo viên chỉ đọc.
/// </summary>
/// <remarks>
/// Ràng buộc phải cài (§5): trim trước khi lưu và trước khi so trùng · unique
/// <c>email/username/code</c> không phân biệt hoa thường, trả <c>Conflict</c> chứ không phải
/// <c>Validation</c> · <see cref="UpdateAsync"/> <b>không</b> nhận mật khẩu · <c>CreatedAt</c> do
/// server đặt · mật khẩu do quản trị viên cấp thì bật <c>MustChangePassword</c> ·
/// <c>completedExams</c> và <c>averageScore</c> là dữ liệu dẫn xuất, đọc từ
/// <c>IExamAttemptRepository.GetStatsByStudentsAsync</c>, không nhận từ client.
/// <see cref="CheckAvailabilityAsync"/> tồn tại để thay việc frontend tải 500 bản ghi về so sánh
/// tại chỗ (§4.3).
/// <para>
/// Phần <b>ai được gọi</b> không thực thi ở đây mà bằng <c>[Authorize(Roles = ...)]</c> ở tầng
/// Api: service không nên tự phán quyền cho mình, và gom quyền vào một chỗ thì đọc controller là
/// thấy đủ bức tranh.
/// </para>
/// </remarks>
public interface IStudentService
{
    /// <summary>Danh sách có phân trang, lọc theo từ khoá / nhóm / trạng thái.</summary>
    Task<Result<PagedResult<StudentDto>>> ListAsync(
        ListStudentsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Hồ sơ một học sinh. Trả <c>NotFound</c> khi id không tồn tại hoặc không phải học sinh.</summary>
    Task<Result<StudentDto>> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Tạo tài khoản kèm mật khẩu ban đầu. Trùng định danh trả <c>Conflict</c>.</summary>
    Task<Result<StudentDto>> CreateAsync(
        CreateStudentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Cập nhật hồ sơ, nhóm, trạng thái. <b>Không nhận mật khẩu.</b></summary>
    Task<Result<StudentDto>> UpdateAsync(
        int id,
        UpdateStudentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Khoá hoặc mở khoá. Khoá phải xoay <c>SecurityStamp</c> để token còn hạn mất hiệu lực
    /// ngay (§5) — nếu không, học sinh vừa bị khoá vẫn dùng tiếp tới khi token tự hết hạn.
    /// </summary>
    Task<Result<StudentDto>> SetStatusAsync(
        int id,
        UserStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cấp lại mật khẩu khi học sinh quên. Mật khẩu mới chỉ trả về đúng một lần, kèm
    /// <c>MustChangePassword</c>, và mọi phiên đang mở của tài khoản đó bị đá ra.
    /// </summary>
    Task<Result<ResetPasswordResponse>> ResetPasswordAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra email/username/mã học sinh đã có ai dùng chưa.</summary>
    Task<Result<AvailabilityResponse>> CheckAvailabilityAsync(
        AvailabilityQuery query,
        CancellationToken cancellationToken = default);
}
