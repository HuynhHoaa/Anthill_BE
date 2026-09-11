using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Results;

/// <summary>
/// Bang ket qua va bang theo doi giu nguyen dong cua em da roi truong, nen moi dong phai mang trang
/// thai tai khoan — cung chuoi voi StudentDto.Status — de giao dien gan nhan thay vi hien nhu em
/// dang hoc binh thuong.
/// </summary>
public class StudentStatusOnResultsTests
{
    private static readonly DateTime OpenAt = new(2026, 9, 10, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CloseAt = new(2026, 9, 17, 1, 0, 0, DateTimeKind.Utc);

    private static ExamAssignment Assignment(User? student)
    {
        var exam = new Exam
        {
            Id = 1,
            Title = "De chung",
            DurationMinutes = 45,
            TotalQuestions = 9,
            OpenAt = OpenAt,
            CloseAt = CloseAt,
            CreatedByUserId = 2
        };

        return new ExamAssignment { Id = 10, ExamId = 1, StudentId = 3, Exam = exam, Student = student! };
    }

    private static User Student(UserStatus status) => new()
    {
        Id = 3,
        FullName = "Tran Thi Lan",
        Email = "lan.tt@anthill.edu.vn",
        Username = "lan.tt",
        Code = "HS1001",
        PasswordHash = "hash",
        SecurityStamp = "stamp",
        AvatarColor = "#2563eb",
        Role = UserRole.Student,
        Status = status
    };

    [Theory]
    [InlineData(UserStatus.Active, "active")]
    [InlineData(UserStatus.Locked, "locked")]
    [InlineData(UserStatus.PendingDeletion, "pendingDeletion")]
    [InlineData(UserStatus.Deleted, "deleted")]
    public void Dong_theo_doi_va_dong_ket_qua_mang_trang_thai_tai_khoan(UserStatus status, string expected)
    {
        var assignment = Assignment(Student(status));
        var attempt = new ExamAttempt
        {
            Id = 21,
            Status = AssignmentStatus.Graded,
            StartedAt = OpenAt.AddHours(1),
            Assignment = assignment
        };
        assignment.Attempt = attempt;

        assignment.ToDto(OpenAt.AddHours(2)).StudentStatus.Should().Be(expected);
        attempt.ToSummaryDto(canRevealScore: true, totalQuestions: 9).StudentStatus.Should().Be(expected);
    }

    [Fact]
    public void Trang_thai_tai_khoan_khong_lan_voi_trang_thai_luot_lam_bai()
    {
        var assignment = Assignment(Student(UserStatus.PendingDeletion));

        var dto = assignment.ToDto(OpenAt.AddHours(2));

        dto.Status.Should().Be("assigned");
        dto.StudentStatus.Should().Be("pendingDeletion");
    }

    [Fact]
    public void Thieu_navigation_Student_van_dung_duoc_DTO()
    {
        var dto = Assignment(student: null).ToDto(OpenAt.AddHours(2));

        dto.StudentStatus.Should().Be("active");
        dto.StudentName.Should().BeEmpty();
    }
}
