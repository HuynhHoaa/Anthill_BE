using System.Security.Cryptography;

namespace EduQuiz.Application.Common;

/// <summary>
/// Sinh mật khẩu tạm khi quản trị viên reset tài khoản học sinh (§4.3).
/// </summary>
/// <remarks>
/// <para>
/// Dùng <see cref="RandomNumberGenerator"/> chứ không phải <see cref="Random"/>: mật khẩu tạm là
/// thứ mở được tài khoản người khác, mà <c>Random</c> gieo hạt theo thời gian nên hai lần reset
/// gần nhau có thể đoán được của nhau.
/// </para>
/// <para>
/// Bảng ký tự cố ý bỏ <c>0 O o 1 l I</c>: mật khẩu này được <b>đọc lên hoặc chép tay</b> cho học
/// sinh, và một ký tự đọc nhầm thì cả hai bên đều không hiểu vì sao đăng nhập hỏng.
/// </para>
/// </remarks>
public static class PasswordGenerator
{
    private const string Alphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Đủ dài để không dò được, đủ ngắn để gõ lại không sai.</summary>
    public const int DefaultLength = 10;

    public static string Generate(int length = DefaultLength)
    {
        if (length < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Độ dài mật khẩu phải lớn hơn 0.");
        }

        var buffer = new char[length];
        for (var i = 0; i < length; i++)
        {
            // GetInt32 lấy mẫu không thiên lệch — chia dư cho độ dài bảng ký tự thì các ký tự
            // đầu bảng xuất hiện nhiều hơn phần còn lại.
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(buffer);
    }
}
