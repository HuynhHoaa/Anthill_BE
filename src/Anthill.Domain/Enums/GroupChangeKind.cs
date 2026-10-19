namespace Anthill.Domain.Enums;

/// <summary>Loại thay đổi được ghi vào lịch sử của một lớp.</summary>
public enum GroupChangeKind
{
    /// <summary>Lớp ra đời, kèm giáo viên phụ trách đầu tiên.</summary>
    Created,

    /// <summary>Đổi giáo viên phụ trách giữa chừng — bắt buộc có lý do khi lớp đã có người phụ trách.</summary>
    TeacherChanged,

    /// <summary>Khoá lớp (bắt buộc có lý do).</summary>
    Locked,

    /// <summary>Mở khoá lớp.</summary>
    Unlocked
}
