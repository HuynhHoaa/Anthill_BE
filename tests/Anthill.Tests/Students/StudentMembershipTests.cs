using Anthill.Application.Common;
using Anthill.Application.DTOs.StudentGroups;
using Anthill.Application.DTOs.Students;
using Anthill.Application.Mappings;
using Anthill.Application.Validators;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;
using FluentValidation;

namespace Anthill.Tests.Students;

/// <summary>
/// Hoc sinh hoc duoc nhieu lop cung luc: cac tin nhan ve lop trong DTO phai dung ten, theo thu tu, va giao vien chi
/// thay nhung lop minh day.
/// </summary>
public class StudentGroupsDtoTests
{
    private static User Student(params StudentGroup[] groups)
    {
        var student = new User
        {
            Id = 3,
            FullName = "Trần Thị Lan",
            Email = "lan.tt@anthill.edu.vn",
            Username = "lan.tt",
            PasswordHash = "hash",
            SecurityStamp = "stamp",
            AvatarColor = "#2563eb",
            Role = UserRole.Student,
            Code = "HS1001"
        };

        foreach (var group in groups)
        {
            student.Groups.Add(group);
        }

        return student;
    }

    private static StudentGroup Group(int id, string name, bool locked = false) =>
        new() { Id = id, Name = name, IsLocked = locked, CreatedByUserId = 1 };

    [Fact]
    public void Hoc_sinh_chua_co_lop_thi_mang_lop_rong()
    {
        Student().ToStudentDto(0, 0m, 0).Groups.Should().BeEmpty();
    }

    [Fact]
    public void Hoc_sinh_hoc_nhieu_lop_thi_nhan_du_cac_lop_theo_ten()
    {
        var dto = Student(Group(2, "Lớp Toán 10"), Group(1, "Lớp Anh 10", locked: true)).ToStudentDto(0, 0m, 0);

        dto.Groups.Select(group => group.Name).Should().Equal("Lớp Anh 10", "Lớp Toán 10");
        dto.Groups.Single(group => group.Id == 1).IsLocked.Should().BeTrue();
        dto.Groups.Single(group => group.Id == 2).IsLocked.Should().BeFalse();
    }

    [Fact]
    public void Giao_vien_chi_nhan_nhung_lop_minh_day()
    {
        var student = Student(Group(1, "Lớp của mình"), Group(2, "Lớp của đồng nghiệp"));

        var dto = student.ToStudentDto(0, 0m, 0, visibleGroupIds: new HashSet<int> { 1 });

        dto.Groups.Should().ContainSingle().Which.Name.Should().Be("Lớp của mình");
    }

    [Fact]
    public void Quan_tri_vien_khong_bi_gioi_han_lop()
    {
        var student = Student(Group(1, "A"), Group(2, "B"));

        student.ToStudentDto(0, 0m, 0, visibleGroupIds: null).Groups.Should().HaveCount(2);
    }

    /// <summary>Đường ghi nạp tài khoản trần nên truyền thẳng các lớp thay vì dựa vào navigation.</summary>
    [Fact]
    public void Truyen_thang_cac_lop_thi_khong_doc_navigation()
    {
        var dto = Student().ToStudentDto(0, 0m, 0, groups: [Group(5, "Lớp mới")]);

        dto.Groups.Should().ContainSingle().Which.Id.Should().Be(5);
    }

    [Fact]
    public void Ho_so_hoc_sinh_chi_co_lop_voi_hoc_sinh()
    {
        Student(Group(1, "A")).ToProfileDto().Groups.Should().ContainSingle();

        var teacher = Student();
        teacher.Role = UserRole.Teacher;
        teacher.ToProfileDto().Groups.Should().BeNull();
    }
}

/// <summary>Khoa lop bat buoc ly do, cung bo luat 10–512 ky tu voi moi thao tac to chuc khac.</summary>
public class LockGroupRequestValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ngắn quá")]
    public void Khoa_lop_thieu_hoac_ngan_ly_do_bi_tu_choi(string reason)
    {
        new LockGroupRequestValidator().Validate(new LockGroupRequest { Reason = reason }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Khoa_lop_khong_co_ly_do_noi_ro_can_nhap_ly_do()
    {
        new LockGroupRequestValidator().Validate(new LockGroupRequest { Reason = " " }).Errors
            .Select(failure => failure.ErrorMessage).Should().Contain("Vui lòng nhập lý do khoá lớp.");
    }

    [Fact]
    public void Khoa_lop_voi_ly_do_qua_dai_bi_tu_choi()
    {
        new LockGroupRequestValidator().Validate(new LockGroupRequest { Reason = new string('a', 513) }).IsValid
            .Should().BeFalse();
    }

    [Fact]
    public void Khoa_lop_voi_ly_do_hop_le_qua_duoc()
    {
        new LockGroupRequestValidator()
            .Validate(new LockGroupRequest { Reason = "Kết thúc học kỳ một, lớp không nhận thêm học sinh" }).IsValid
            .Should().BeTrue();
    }
}

/// <summary>Lich su cua lop: ten loai viet thuong chu dau (camelCase), gom ca khoa va mo khoa.</summary>
public class GroupChangeMappingTests
{
    [Theory]
    [InlineData(GroupChangeKind.Created, "created")]
    [InlineData(GroupChangeKind.TeacherChanged, "teacherChanged")]
    [InlineData(GroupChangeKind.Locked, "locked")]
    [InlineData(GroupChangeKind.Unlocked, "unlocked")]
    public void Ten_loai_thay_doi_cua_lop_la_camelCase(GroupChangeKind kind, string expected)
    {
        new GroupChange { Kind = kind }.ToDto().Kind.Should().Be(expected);
    }

    [Fact]
    public void Lop_khoa_mang_co_khoa_va_moc_thoi_gian()
    {
        var locked = new StudentGroup
        {
            Name = "10A1",
            CreatedByUserId = 1,
            IsLocked = true,
            LockedAt = new DateTime(2026, 10, 19, 1, 0, 0, DateTimeKind.Utc)
        };

        var dto = locked.ToDto(studentCount: 12);

        dto.IsLocked.Should().BeTrue();
        dto.LockedAt.Should().Be(new DateTime(2026, 10, 19, 1, 0, 0, DateTimeKind.Utc));
        new StudentGroup { Name = "10A2", CreatedByUserId = 1 }.ToDto(0).IsLocked.Should().BeFalse();
    }
}

/// <summary>
/// Khoa tai khoan, xoa tai khoan, go khoi lop va chuyen lop deu bat buoc co ly do, cung mot bo luat (10–512 ky tu
/// sau khi cat khoang trang).
/// </summary>
public class MembershipReasonValidatorTests
{
    private const string ValidReason = "Học sinh đã nghỉ học từ đầu tháng";

    private static IReadOnlyList<string> ErrorsOf<T>(IValidator<T> validator, T request) =>
        validator.Validate(request).Errors.Select(failure => failure.ErrorMessage).ToList();

    [Fact]
    public void Khoa_tai_khoan_ma_khong_co_ly_do_bi_tu_choi()
    {
        var request = new UpdateStudentStatusRequest { Status = "locked" };

        ErrorsOf(new UpdateStudentStatusRequestValidator(), request)
            .Should().Contain("Vui lòng nhập lý do khoá tài khoản.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Khoa_tai_khoan_voi_ly_do_trong_bi_tu_choi(string reason)
    {
        var request = new UpdateStudentStatusRequest { Status = "locked", Reason = reason };

        new UpdateStudentStatusRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Khoa_tai_khoan_voi_ly_do_qua_ngan_bi_tu_choi_do_tren_ban_da_cat_khoang_trang()
    {
        var request = new UpdateStudentStatusRequest { Status = "locked", Reason = "a" + new string(' ', 9) };

        ErrorsOf(new UpdateStudentStatusRequestValidator(), request)
            .Should().Contain(message => message.Contains("ít nhất"));
    }

    [Fact]
    public void Khoa_tai_khoan_voi_ly_do_dai_hon_512_ky_tu_bi_tu_choi()
    {
        var request = new UpdateStudentStatusRequest { Status = "locked", Reason = new string('a', 513) };

        ErrorsOf(new UpdateStudentStatusRequestValidator(), request)
            .Should().Contain(message => message.Contains("tối đa"));
    }

    [Fact]
    public void Khoa_tai_khoan_voi_ly_do_hop_le_qua_duoc()
    {
        var request = new UpdateStudentStatusRequest { Status = "locked", Reason = ValidReason };

        new UpdateStudentStatusRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    /// <summary>Tra lai quyen dang nhap thi khong ai can giai thich, nen khong doi ly do.</summary>
    [Fact]
    public void Mo_khoa_khong_can_ly_do()
    {
        var request = new UpdateStudentStatusRequest { Status = "active" };

        new UpdateStudentStatusRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Trang_thai_la_van_bi_tu_choi_nhu_truoc()
    {
        var request = new UpdateStudentStatusRequest { Status = "deleted", Reason = ValidReason };

        ErrorsOf(new UpdateStudentStatusRequestValidator(), request)
            .Should().Contain("Trạng thái tài khoản chỉ nhận giá trị 'active' hoặc 'locked'.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("          ")]
    [InlineData("ngắn quá")]
    public void Go_khoi_lop_ma_ly_do_thieu_hoac_ngan_bi_tu_choi(string? reason)
    {
        var request = new RemoveMemberRequest { Reason = reason! };

        new RemoveMemberRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Go_khoi_lop_voi_ly_do_hop_le_qua_duoc()
    {
        new RemoveMemberRequestValidator().Validate(new RemoveMemberRequest { Reason = ValidReason }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Chuyen_lop_ma_chua_chon_lop_dich_bi_tu_choi(int target)
    {
        var request = new TransferMemberRequest { TargetGroupId = target, Reason = ValidReason };

        ErrorsOf(new TransferMemberRequestValidator(), request)
            .Should().Contain("Hãy chọn lớp muốn chuyển em sang.");
    }

    [Fact]
    public void Chuyen_lop_ma_khong_co_ly_do_bi_tu_choi()
    {
        var request = new TransferMemberRequest { TargetGroupId = 4, Reason = " " };

        ErrorsOf(new TransferMemberRequestValidator(), request)
            .Should().Contain("Vui lòng nhập lý do chuyển lớp.");
    }

    [Fact]
    public void Chuyen_lop_hop_le_qua_duoc()
    {
        var request = new TransferMemberRequest { TargetGroupId = 4, Reason = ValidReason };

        new TransferMemberRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    /// <summary>Ly do xoa tai khoan da co tu truoc; gop ve mot bo luat khong duoc doi hanh vi cua no.</summary>
    [Fact]
    public void Ly_do_xoa_tai_khoan_van_theo_cung_bo_luat()
    {
        new RequestDeletionRequestValidator().Validate(new RequestDeletionRequest { Reason = ValidReason }).IsValid
            .Should().BeTrue();
        new RequestDeletionRequestValidator().Validate(new RequestDeletionRequest { Reason = "ngắn" }).IsValid
            .Should().BeFalse();
        RequestDeletionRequestValidator.MinimumReasonLength.Should().Be(ReasonRules.MinimumLength);
    }
}

/// <summary>Lich su cua hoc sinh: ten loai viet thuong chu dau (camelCase) de dung khuon cac chuoi trang thai khac.</summary>
public class AccountChangeMappingTests
{
    [Theory]
    [InlineData(AccountChangeKind.Locked, "locked")]
    [InlineData(AccountChangeKind.Unlocked, "unlocked")]
    [InlineData(AccountChangeKind.DeletionRequested, "deletionRequested")]
    [InlineData(AccountChangeKind.Restored, "restored")]
    [InlineData(AccountChangeKind.AddedToGroup, "addedToGroup")]
    [InlineData(AccountChangeKind.RemovedFromGroup, "removedFromGroup")]
    [InlineData(AccountChangeKind.MovedGroup, "movedGroup")]
    public void Ten_loai_thay_doi_la_camelCase(AccountChangeKind kind, string expected)
    {
        new AccountChange { Kind = kind }.ToDto().Kind.Should().Be(expected);
    }

    [Fact]
    public void Dong_lich_su_mang_ly_do_ten_lop_luc_xay_ra_va_nguoi_thuc_hien()
    {
        var change = new AccountChange
        {
            Id = 7,
            Kind = AccountChangeKind.MovedGroup,
            Reason = "Xếp nhầm lớp",
            FromGroupName = "Lớp 10A1",
            ToGroupName = "Lớp 10A2",
            Actor = new User
            {
                FullName = "Phạm Quang Huy",
                Email = "huy.pq@anthill.edu.vn",
                Username = "huy.pq",
                PasswordHash = "hash",
                SecurityStamp = "stamp",
                AvatarColor = "#2563eb"
            },
            CreatedAt = new DateTime(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc)
        };

        var dto = change.ToDto();

        dto.Reason.Should().Be("Xếp nhầm lớp");
        dto.FromGroup.Should().Be("Lớp 10A1");
        dto.ToGroup.Should().Be("Lớp 10A2");
        dto.ActorName.Should().Be("Phạm Quang Huy");
        dto.CreatedAt.Should().Be(new DateTime(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Nguoi_thuc_hien_da_bi_go_thi_dong_lich_su_van_doc_duoc()
    {
        new AccountChange { Kind = AccountChangeKind.Locked, Reason = "Nghỉ học không báo" }.ToDto()
            .ActorName.Should().BeNull();
    }
}

/// <summary>
/// Trang hoc sinh cua quan tri vien phai thay ca bai em chua lam: em nghi hoc ma chua bi go khoi lop thi cac bai sau
/// van hien ra voi trang thai chua lam hoac het han.
/// </summary>
public class StudentExamDtoTests
{
    private static readonly DateTime Open = new(2026, 9, 10, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Close = new(2026, 9, 10, 3, 0, 0, DateTimeKind.Utc);

    private static ExamAssignment Assignment(bool showResult, ExamAttempt? attempt = null) => new()
    {
        Id = 10,
        ExamId = 1,
        StudentId = 3,
        AssignedViaGroupId = 5,
        AssignedViaGroup = new StudentGroup { Id = 5, Name = "Lớp 10A1", CreatedByUserId = 1 },
        Attempt = attempt,
        Exam = new Exam
        {
            Id = 1,
            Title = "Kiểm tra 15 phút",
            DurationMinutes = 15,
            TotalQuestions = 9,
            OpenAt = Open,
            CloseAt = Close,
            IsDraft = false,
            ShowResultAfterSubmit = showResult,
            CreatedByUserId = 2,
            CreatedBy = new User
            {
                FullName = "Nguyễn Văn An",
                Email = "an.nv@anthill.edu.vn",
                Username = "an.nv",
                PasswordHash = "hash",
                SecurityStamp = "stamp",
                AvatarColor = "#2563eb"
            }
        }
    };

    [Fact]
    public void Bai_em_chua_lam_va_da_het_han_hien_la_expired_khong_co_diem()
    {
        var dto = Assignment(showResult: true).ToStudentExamDto(Close.AddHours(1));

        dto.Status.Should().Be("expired");
        dto.AttemptId.Should().BeNull();
        dto.Score.Should().BeNull();
        dto.Group.Should().Be("Lớp 10A1");
        dto.TeacherName.Should().Be("Nguyễn Văn An");
    }

    [Fact]
    public void Bai_em_chua_lam_con_han_hien_la_assigned()
    {
        Assignment(showResult: true).ToStudentExamDto(Open.AddMinutes(30)).Status.Should().Be("assigned");
    }

    /// <summary>
    /// Co "hien ket qua sau khi nop" chi noi ve dieu hoc sinh duoc thay. Quan tri vien xem nhu nguoi quan sat nen diem
    /// luon hien khi bai da cham.
    /// </summary>
    [Fact]
    public void Diem_luon_hien_cho_quan_tri_vien_ke_ca_khi_de_an_ket_qua_voi_hoc_sinh()
    {
        var attempt = new ExamAttempt
        {
            Id = 99,
            AssignmentId = 10,
            Status = AssignmentStatus.Graded,
            Score = 8m,
            MaxScore = 10m,
            CorrectCount = 8,
            StartedAt = Open.AddMinutes(5),
            SubmittedAt = Open.AddMinutes(20)
        };

        var dto = Assignment(showResult: false, attempt).ToStudentExamDto(Close.AddHours(1));

        dto.Status.Should().Be("graded");
        dto.AttemptId.Should().Be(99);
        dto.Score.Should().Be(8m);
        dto.MaxScore.Should().Be(10m);
        dto.CorrectCount.Should().Be(8);
    }

    [Fact]
    public void Bai_dang_lam_do_khong_tra_diem_0_nhu_bai_sai_het()
    {
        var attempt = new ExamAttempt
        {
            Id = 100,
            AssignmentId = 10,
            Status = AssignmentStatus.InProgress,
            StartedAt = Open.AddMinutes(5)
        };

        var dto = Assignment(showResult: true, attempt).ToStudentExamDto(Open.AddMinutes(10));

        dto.Status.Should().Be("inprogress");
        dto.Score.Should().BeNull();
    }
}
