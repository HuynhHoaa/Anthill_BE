namespace Anthill.Application.Common;

/// <summary>Giới hạn chung của mọi lần nhập từ file Excel.</summary>
public static class ImportLimits
{
    /// <summary>
    /// Trần dung lượng file Excel. Năm nghìn dòng học sinh chỉ vài trăm KB; 5 MB đủ cho file có định dạng, và đủ nhỏ để
    /// một file khác (ảnh chụp, bản sao lưu) lạc vào đây bị từ chối sớm.
    /// </summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>Phần dư cho vỏ multipart và các trường kèm theo (mật khẩu, lớp mặc định).</summary>
    public const long MultipartOverheadBytes = 64 * 1024;

    /// <summary>Trần cả thân request: file đúng <see cref="MaxFileBytes"/> cộng vỏ multipart.</summary>
    public const long MaxRequestBytes = MaxFileBytes + MultipartOverheadBytes;

    /// <summary>Một câu cho mọi đường từ chối file quá lớn, để người dùng không thấy hai thông điệp cho cùng một lỗi.</summary>
    public const string FileTooLargeMessage = "File Excel quá lớn (tối đa 5 MB). Hãy tách thành nhiều file.";
}
