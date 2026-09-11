using Anthill.Domain.Entities;
using Anthill.Domain.Enums;

namespace Anthill.Application.Common;

/// <summary>Lý do một học sinh được chọn nhưng không được giao bài.</summary>
public enum AssignmentSkipReason
{
    /// <summary>Giao được.</summary>
    None,

    /// <summary>Em không thuộc lớp nào mà giáo viên này được phân công dạy.</summary>
    OutOfScope,

    /// <summary>Tài khoản đang khoá — vẫn là học sinh của trường, mở khoá là giao lại được.</summary>
    Locked,

    /// <summary>Tài khoản chờ xoá hoặc đã xoá — em đã rời trường.</summary>
    Retired
}

/// <summary>
/// Sàng từng học sinh trong một lần giao bài. Hàm thuần, tách khỏi service để kiểm bằng unit test.
/// </summary>
/// <remarks>
/// <para>
/// Thứ tự sàng có ý nghĩa: <b>ngoài phạm vi</b> → <b>đã rời trường</b> → <b>đang khoá</b>. Mỗi em chỉ
/// được đếm một lần, ở lý do đầu tiên chạm tới — nếu không, tổng các con số bỏ qua sẽ lớn hơn số em
/// được chọn và bảng kết quả đọc vào vô nghĩa.
/// </para>
/// <para>
/// Tách "đã rời trường" khỏi "đang khoá" vì hai lý do dẫn tới hai hành động khác nhau: em bị khoá thì
/// giáo viên nhờ quản trị viên mở khoá rồi giao lại; em chờ xoá thì không có gì để làm. Gộp chung thì
/// màn hình báo "1 em đang bị khoá" cho một em đã chuyển trường, và giáo viên đi hỏi một việc không
/// tồn tại.
/// </para>
/// </remarks>
public static class AssignmentTargetFilter
{
    /// <param name="student">Tài khoản học sinh có thật — id lạ đã được đếm riêng trước khi tới đây.</param>
    /// <param name="teachableGroupIds">Các lớp mà giáo viên đang giao bài được phân công dạy.</param>
    public static AssignmentSkipReason Classify(User student, IReadOnlySet<int> teachableGroupIds)
    {
        ArgumentNullException.ThrowIfNull(student);
        ArgumentNullException.ThrowIfNull(teachableGroupIds);

        // Phạm vi đứng trước trạng thái: em của lớp khác thì trạng thái tài khoản của em không phải
        // chuyện giáo viên này được biết, kể cả qua một con số đếm.
        if (student.GroupId is not { } groupId || !teachableGroupIds.Contains(groupId))
        {
            return AssignmentSkipReason.OutOfScope;
        }

        return student.Status switch
        {
            UserStatus.Active => AssignmentSkipReason.None,
            UserStatus.PendingDeletion or UserStatus.Deleted => AssignmentSkipReason.Retired,

            // Tài khoản khoá thì không đăng nhập được, nên tạo bài cho em cũng chỉ tạo ra một dòng
            // không ai dùng tới. Frontend hiện vẫn cho chọn và vẫn tạo.
            _ => AssignmentSkipReason.Locked
        };
    }
}
