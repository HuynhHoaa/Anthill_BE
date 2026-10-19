using Anthill.Domain.Common;
using Anthill.Domain.Enums;

namespace Anthill.Domain.Entities;

/// <summary>
/// Một dòng trong lịch sử của một tài khoản (học sinh hay giáo viên): ai đã làm gì với tài khoản hay lớp, lúc nào, vì sao.
/// </summary>
/// <remarks>
/// <para>
/// <b>Chỉ thêm, không sửa, không xoá.</b> Lý do là thứ quản trị viên gõ để người đọc sau này hiểu vì sao
/// một em bị khoá hay chuyển lớp; sửa lại được thì nó không còn là bằng chứng.
/// </para>
/// <para>
/// Tên lớp được chép lại (<see cref="FromGroupName"/>, <see cref="ToGroupName"/>) thay vì chỉ giữ khoá
/// ngoại: lớp có thể đổi tên hoặc bị xoá khi đã trống, còn dòng lịch sử phải đọc ra đúng như lúc nó xảy
/// ra. Vì vậy hai cột mã lớp cố ý không có ràng buộc khoá ngoại.
/// </para>
/// </remarks>
public class AccountChange : BaseEntity
{
    /// <summary>Tài khoản bị tác động: học sinh (khoá, xoá, đổi lớp) hoặc giáo viên (khoá, mở khoá).</summary>
    public int AccountId { get; set; }

    public User Account { get; set; } = null!;

    public AccountChangeKind Kind { get; set; }

    /// <summary>Lý do do quản trị viên nhập. NULL với các loại không bắt buộc lý do.</summary>
    public string? Reason { get; set; }

    public int? FromGroupId { get; set; }

    public string? FromGroupName { get; set; }

    public int? ToGroupId { get; set; }

    public string? ToGroupName { get; set; }

    /// <summary>Quản trị viên thực hiện. Có thể NULL nếu tài khoản đó bị gỡ khỏi hệ thống sau này.</summary>
    public int? ActorUserId { get; set; }

    public User? Actor { get; set; }
}
