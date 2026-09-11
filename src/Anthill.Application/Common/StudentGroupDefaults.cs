namespace Anthill.Application.Common;

/// <summary>
/// Cầu nối giữa cách database diễn đạt "chưa thuộc nhóm nào" và cách frontend diễn đạt điều đó.
/// </summary>
/// <remarks>
/// <para>
/// Database dùng <c>User.GroupId = NULL</c>. Frontend khai báo <c>Student.groupId: number</c>
/// — không nullable — và quy ước <c>UNASSIGNED_GROUP_ID = 0</c> kèm nhãn "Chưa phân nhóm"
/// (<c>src/services/groupService.ts</c>).
/// </para>
/// <para>
/// Đổi DTO sang <c>int?</c> cho khớp database nghĩa là sửa type ở frontend, rồi sửa mọi chỗ so
/// sánh <c>student.groupId === group.id</c> — trái mục tiêu "chữ ký hàm giữ nguyên nên không
/// trang nào phải sửa". Nên phép quy đổi nằm gọn ở đây: NULL ↔ 0, và chỉ ở đây.
/// </para>
/// </remarks>
public static class StudentGroupDefaults
{
    /// <summary>Giá trị frontend dùng cho học sinh chưa thuộc nhóm nào.</summary>
    public const int UnassignedId = 0;

    /// <summary>Nhãn hiển thị đi kèm, khớp <c>UNASSIGNED_GROUP_NAME</c> của frontend.</summary>
    public const string UnassignedName = "Chưa phân nhóm";

    /// <summary>
    /// Từ giá trị client gửi lên sang giá trị lưu xuống cột <c>GroupId</c>.
    /// Cả <c>null</c> lẫn <c>0</c> đều nghĩa là "không thuộc nhóm nào".
    /// </summary>
    public static int? ToStoredGroupId(int? clientGroupId) =>
        clientGroupId is null or UnassignedId ? null : clientGroupId;

    /// <summary>Từ cột <c>GroupId</c> sang giá trị frontend đọc được.</summary>
    public static int ToClientGroupId(int? storedGroupId) => storedGroupId ?? UnassignedId;

    /// <summary>Tên nhóm cho DTO: thiếu nhóm thì trả nhãn, không trả chuỗi rỗng.</summary>
    public static string ToClientGroupName(string? groupName) =>
        string.IsNullOrWhiteSpace(groupName) ? UnassignedName : groupName;
}
