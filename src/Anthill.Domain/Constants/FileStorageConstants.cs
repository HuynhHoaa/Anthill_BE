namespace Anthill.Domain.Constants;

/// <summary>
/// Hằng số cho luồng ảnh câu hỏi/đáp án, dùng chung giữa tầng Application (kiểm tra
/// <c>ImageUrl</c> gửi lên) và tầng Infrastructure (nơi thật sự phục vụ file tĩnh), vì cả hai
/// đều phải đồng ý với nhau về hình dạng của một URL ảnh hợp lệ.
/// </summary>
public static class FileStorageConstants
{
    /// <summary>
    /// Tiền tố URL công khai mặc định cho ảnh đã upload. Validator ở tầng Application dùng đúng
    /// giá trị này để từ chối mọi <c>ImageUrl</c> không phải do <c>/api/uploads/images</c> cấp —
    /// một URL tuyệt đối tới máy chủ khác thì trình duyệt của mọi học sinh sẽ tải nó lúc làm bài,
    /// lộ IP và thời điểm làm bài cho bên đó.
    /// </summary>
    public const string PublicRequestPathPrefix = "/uploads";
}
