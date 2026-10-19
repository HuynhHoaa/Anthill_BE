namespace Anthill.Application.Common;

/// <summary>
/// Cầu nối giữa cách giao diện diễn đạt "chưa thuộc lớp nào" ở bộ lọc và cách database diễn đạt điều đó.
/// </summary>
/// <remarks>
/// Frontend dùng <c>UNASSIGNED_GROUP_ID = 0</c> cho ô lọc "Chưa phân nhóm"; database không có lớp nào mang id 0, và "chưa có
/// lớp" là <b>không có dòng thành viên nào</b>. Phép quy đổi nằm gọn ở đây.
/// </remarks>
public static class StudentGroupDefaults
{
    /// <summary>Giá trị frontend dùng cho học sinh chưa thuộc lớp nào.</summary>
    public const int UnassignedId = 0;

    /// <summary>
    /// Từ <c>groupId</c> client gửi lên sang id lớp thật. Cả <c>null</c> lẫn <c>0</c> đều nghĩa là "không chọn lớp nào".
    /// </summary>
    public static int? ToStoredGroupId(int? clientGroupId) =>
        clientGroupId is null or UnassignedId ? null : clientGroupId;
}
