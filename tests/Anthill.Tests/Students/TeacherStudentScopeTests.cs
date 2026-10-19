using Anthill.Application.Common;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Students;

/// <summary>
/// Giáo viên chỉ nhìn thấy học sinh mình phụ trách.
/// </summary>
/// <remarks>
/// Test biên dịch đúng biểu thức mà repository ghép vào truy vấn EF, nên thứ được kiểm ở đây là thứ
/// chạy thành SQL — không phải một bản viết lại cho vừa bài test. Phần dịch sang SQL thì bộ kiểm HTTP
/// <c>teacher-student-scope.sh</c> lo.
/// </remarks>
public class TeacherStudentScopeTests
{
    private const int TeacherId = 7;
    private const int OtherTeacherId = 8;

    private static bool InList(User student) => TeacherStudentScope.InTaughtClass(TeacherId).Compile()(student);

    private static bool CanOpen(User student) => TeacherStudentScope.CanReadProfile(TeacherId).Compile()(student);

    [Fact]
    public void Hoc_sinh_lop_minh_day_da_cong_bo_thi_thay_ca_danh_sach_lan_ho_so()
    {
        var student = StudentIn(Class(published: true, TeacherId));

        InList(student).Should().BeTrue();
        CanOpen(student).Should().BeTrue();
    }

    [Fact]
    public void Lop_con_la_ban_nhap_thi_chua_ai_day_ca_ke_ca_nguoi_duoc_phan_cong()
    {
        var student = StudentIn(Class(published: false, TeacherId));

        InList(student).Should().BeFalse();
        CanOpen(student).Should().BeFalse();
    }

    [Fact]
    public void Lop_cua_dong_nghiep_thi_khong_thay()
    {
        var student = StudentIn(Class(published: true, OtherTeacherId));

        InList(student).Should().BeFalse();
        CanOpen(student).Should().BeFalse();
    }

    /// <summary>Lớp chưa có ai dạy vẫn công bố được — và không vì thế mà thuộc về mọi giáo viên.</summary>
    [Fact]
    public void Lop_chua_phan_cong_ai_thi_khong_thuoc_giao_vien_nao()
    {
        var student = StudentIn(Class(published: true));

        InList(student).Should().BeFalse();
        CanOpen(student).Should().BeFalse();
    }

    /// <summary>
    /// Ô lọc "Chưa phân nhóm" của giáo viên rỗng là hệ quả của quy tắc này, không phải một nhánh riêng.
    /// </summary>
    [Fact]
    public void Hoc_sinh_chua_phan_lop_khong_thuoc_giao_vien_nao()
    {
        var student = StudentIn(group: null);

        InList(student).Should().BeFalse();
        CanOpen(student).Should().BeFalse();
    }

    /// <summary>
    /// Một em học được nhiều lớp cùng lúc: chỉ cần một lớp (đã công bố) do giáo viên này dạy là em nằm trong danh sách, và lớp
    /// của đồng nghiệp mà em cùng học không làm em biến mất.
    /// </summary>
    [Fact]
    public void Em_hoc_hai_lop_thi_chi_can_mot_lop_cua_minh_la_thay()
    {
        var student = StudentIn(Class(published: true, OtherTeacherId), Class(published: true, TeacherId));

        InList(student).Should().BeTrue();
        CanOpen(student).Should().BeTrue();
    }

    [Fact]
    public void Em_hoc_hai_lop_ma_lop_cua_minh_con_la_ban_nhap_thi_khong_thay()
    {
        var student = StudentIn(Class(published: true, OtherTeacherId), Class(published: false, TeacherId));

        InList(student).Should().BeFalse();
        CanOpen(student).Should().BeFalse();
    }

    [Fact]
    public void Lop_nhieu_giao_vien_thi_ai_duoc_phan_cong_cung_thay()
    {
        var student = StudentIn(Class(published: true, OtherTeacherId, TeacherId));

        InList(student).Should().BeTrue();
    }

    /// <summary>
    /// Em chuyển sang lớp khác sau khi đã nhận đề của giáo viên này: bấm tên em từ trang kết quả vẫn
    /// phải mở được hồ sơ, nhưng em không còn nằm trong danh sách lớp của giáo viên nữa.
    /// </summary>
    [Fact]
    public void Em_da_nhan_de_cua_minh_roi_chuyen_lop_thi_mo_duoc_ho_so_nhung_khong_con_trong_danh_sach()
    {
        var student = StudentIn(Class(published: true, OtherTeacherId));
        student.Assignments.Add(AssignmentOfExamBy(TeacherId));

        InList(student).Should().BeFalse();
        CanOpen(student).Should().BeTrue();
    }

    /// <summary>Tài khoản đã xoá dữ liệu thường không còn lớp, nhưng điểm vẫn ở trong bảng kết quả.</summary>
    [Fact]
    public void Em_da_roi_lop_van_mo_duoc_ho_so_neu_tung_nhan_de_cua_minh()
    {
        var student = StudentIn(group: null);
        student.Status = UserStatus.Deleted;
        student.Assignments.Add(AssignmentOfExamBy(TeacherId));

        CanOpen(student).Should().BeTrue();
    }

    /// <summary>Đề của đồng nghiệp không mở cửa hồ sơ: kết quả của đề đó không nằm trong bảng của mình.</summary>
    [Fact]
    public void Nhan_de_cua_dong_nghiep_khong_mo_duoc_ho_so()
    {
        var student = StudentIn(Class(published: true, OtherTeacherId));
        student.Assignments.Add(AssignmentOfExamBy(OtherTeacherId));

        CanOpen(student).Should().BeFalse();
    }

    private static StudentGroup Class(bool published, params int[] teacherIds)
    {
        var group = new StudentGroup { Name = "12A1", IsPublished = published };
        foreach (var teacherId in teacherIds)
        {
            group.Teachers.Add(new StudentGroupTeacher { TeacherId = teacherId });
        }

        return group;
    }

    private static User StudentIn(StudentGroup? group) => new()
    {
        FullName = "Trần Thị Lan",
        Email = "lan@anthill.edu.vn",
        Username = "lan.tt",
        PasswordHash = "x",
        SecurityStamp = "x",
        AvatarColor = "#6366F1",
        Role = UserRole.Student,
        Groups = group is null ? new List<StudentGroup>() : new List<StudentGroup> { group }
    };

    private static User StudentIn(params StudentGroup[] groups)
    {
        var student = StudentIn(group: null);
        foreach (var group in groups)
        {
            student.Groups.Add(group);
        }

        return student;
    }

    private static ExamAssignment AssignmentOfExamBy(int teacherId) => new()
    {
        Exam = new Exam { Title = "Kiểm tra 15 phút", CreatedByUserId = teacherId }
    };
}
