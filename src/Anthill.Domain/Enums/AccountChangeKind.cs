namespace Anthill.Domain.Enums;

/// <summary>
/// Loại thay đổi được ghi vào lịch sử của một học sinh (<c>AccountChange</c>).
/// </summary>
/// <remarks>
/// Chỉ những việc quản trị viên làm trên tài khoản và lớp của em mới có mặt ở đây — khoá, xoá, gỡ khỏi
/// lớp, chuyển lớp đều bắt buộc kèm lý do. Tên được lưu thành chuỗi trong database, nên đổi tên hằng số
/// là đổi dữ liệu đã ghi.
/// </remarks>
public enum AccountChangeKind
{
    /// <summary>Khoá tài khoản. Luôn có lý do.</summary>
    Locked,

    /// <summary>Mở khoá tài khoản. Không bắt buộc lý do.</summary>
    Unlocked,

    /// <summary>Đưa vào danh sách chờ xoá. Luôn có lý do.</summary>
    DeletionRequested,

    /// <summary>Khôi phục tài khoản đang chờ xoá.</summary>
    Restored,

    /// <summary>Xếp vào lớp từ trạng thái chưa có lớp. Không bắt buộc lý do.</summary>
    AddedToGroup,

    /// <summary>Gỡ khỏi lớp. Luôn có lý do.</summary>
    RemovedFromGroup,

    /// <summary>Chuyển từ lớp này sang lớp khác. Luôn có lý do.</summary>
    MovedGroup
}
