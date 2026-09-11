using System.Linq.Expressions;
using Anthill.Domain.Entities;

namespace Anthill.Application.Common;

/// <summary>
/// Giáo viên được nhìn thấy những học sinh nào — quy tắc duy nhất, dùng chung cho SQL và unit test.
/// </summary>
/// <remarks>
/// <para>
/// Viết dưới dạng <see cref="Expression{TDelegate}"/> chứ không phải hàm C# thường vì hai lẽ. Repository
/// ghép thẳng nó vào truy vấn EF, nên phạm vi được lọc trong SQL — không nạp cả trường về rồi lọc
/// trong bộ nhớ, thứ vừa chậm dần theo từng khoá học sinh vừa dễ quên ở một chỗ gọi nào đó. Còn unit
/// test biên dịch cùng biểu thức đó và chạy trên đối tượng trong bộ nhớ, nên thứ được kiểm chính là
/// thứ chạy ở database, không phải một bản sao viết lại.
/// </para>
/// <para>
/// Quản trị viên không đi qua đây: họ sở hữu tài khoản của cả tổ chức nên không có phạm vi nào để lọc.
/// </para>
/// </remarks>
public static class TeacherStudentScope
{
    /// <summary>
    /// Học sinh đang ngồi trong một lớp mà giáo viên này được phân công dạy <b>và</b> lớp đã công bố.
    /// Đây là phạm vi của <b>danh sách</b>.
    /// </summary>
    /// <remarks>
    /// Cùng hai điều kiện với <c>StudentGroupRepository.GetForTeacherAsync</c>: danh sách học sinh và
    /// danh sách lớp của giáo viên phải nói cùng một câu, nếu không màn hình giao bài hiện ra những em
    /// mà bước giao bài sẽ loại vì "ngoài phạm vi". Lớp còn là bản nháp thì chưa ai dạy nó cả, kể cả
    /// người đã được phân công. Học sinh chưa phân lớp không thuộc giáo viên nào — vì thế ô lọc
    /// "Chưa phân nhóm" của giáo viên luôn rỗng mà không cần nhánh riêng.
    /// </remarks>
    public static Expression<Func<User, bool>> InTaughtClass(int teacherId) =>
        student => student.Group != null
                   && student.Group.IsPublished
                   && student.Group.Teachers.Any(assignment => assignment.TeacherId == teacherId);

    /// <summary>
    /// Phạm vi của <b>hồ sơ một học sinh</b>: như <see cref="InTaughtClass"/>, cộng thêm mọi em đã
    /// từng nhận một đề do chính giáo viên này tạo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vế thứ hai tồn tại vì bảng kết quả. Kết quả thuộc về đề, không thuộc về lớp hiện tại của em:
    /// em chuyển lớp giữa kỳ, hay tài khoản bị xoá dữ liệu cá nhân và rời lớp, thì bài đã làm vẫn nằm
    /// trong bảng điểm của giáo viên ra đề. Chỉ dùng vế lớp học thì bấm vào tên em đó từ trang kết quả
    /// sẽ gặp "không tìm thấy" — giáo viên thấy điểm của một người mà không mở được hồ sơ người ấy.
    /// </para>
    /// <para>
    /// Vế này cố ý <b>không</b> áp cho danh sách: danh sách là "lớp tôi đang dạy", còn em đã chuyển
    /// đi thì không còn trong lớp nào của giáo viên nữa. Hai vế phải giữ đúng như
    /// <see cref="InTaughtClass"/> — biểu thức không ghép được với nhau nếu không viết thêm một bộ
    /// duyệt cây, nên vế đầu được chép lại, và unit test ghim cả hai.
    /// </para>
    /// </remarks>
    public static Expression<Func<User, bool>> CanReadProfile(int teacherId) =>
        student => (student.Group != null
                    && student.Group.IsPublished
                    && student.Group.Teachers.Any(assignment => assignment.TeacherId == teacherId))
                   || student.Assignments.Any(assignment => assignment.Exam.CreatedByUserId == teacherId);
}
