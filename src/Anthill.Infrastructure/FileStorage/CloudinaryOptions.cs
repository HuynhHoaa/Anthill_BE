namespace Anthill.Infrastructure.FileStorage;

/// <summary>Khoá của một tài khoản Cloudinary. <see cref="ApiSecret"/> là bí mật: chỉ đặt qua biến môi trường.</summary>
public sealed class CloudinaryOptions
{
    public string CloudName { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Thư mục trên Cloudinary chứa ảnh của hệ thống, để khỏi lẫn với ảnh khác trong cùng tài khoản.</summary>
    public string Folder { get; set; } = "anthill";
}
