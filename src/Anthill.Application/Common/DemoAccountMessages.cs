namespace Anthill.Application.Common;

/// <summary>Câu chữ dùng chung ở mọi nơi một thao tác bị chặn vì đụng tài khoản demo (§H19).</summary>
public static class DemoAccountMessages
{
    public const string MutationBlocked =
        "Đây là tài khoản demo dùng chung cho mọi người ghé thử hệ thống, không thể đổi mật khẩu, " +
        "đổi thông tin đăng nhập, khoá, cấp lại mật khẩu hay xoá tài khoản này. Hãy tạo một tài " +
        "khoản riêng để thử các thao tác đó.";
}
