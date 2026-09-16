namespace Anthill.Application.Interfaces.Realtime;

/// <summary>
/// Cắt ngay mọi kết nối realtime đang mở của một tài khoản.
/// </summary>
/// <remarks>
/// <para>
/// Gọi ở mọi chỗ xoay <c>SecurityStamp</c> — khoá, cấp lại mật khẩu, đổi mật khẩu, đưa vào diện xoá,
/// ẩn danh hoá — và chỉ gọi <b>sau khi</b> transaction đã commit. Cắt trước lúc commit thì client tự kết
/// nối lại ngay, negotiate đọc được stamp cũ (thay đổi chưa hiện ra với transaction khác) và kết nối mới
/// sống tiếp như chưa có gì.
/// </para>
/// <para>
/// Phép kiểm phiên ở negotiate và ở từng lời gọi hub vẫn giữ nguyên. Thứ này che phần chúng không che
/// được: sự kiện server <b>đẩy</b> xuống một kết nối đã mở không đi qua phép kiểm nào.
/// </para>
/// </remarks>
public interface IUserConnectionCloser
{
    /// <summary>Cắt mọi kết nối của <paramref name="userId"/> mà tiến trình này đang giữ.</summary>
    /// <returns>Số kết nối vừa cắt.</returns>
    int CloseConnectionsOf(int userId);
}
