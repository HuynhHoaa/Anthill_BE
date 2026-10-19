using Anthill.Application.Common;
using Anthill.Application.DTOs.ExamAttempts;
using FluentAssertions;

namespace Anthill.Tests.Results;

/// <summary>
/// Bang diem cua hoc sinh: diem trung binh tung mon chi tinh bai da den han tinh — bai da nop va duoc xem diem, cong bai het han ma
/// em khong lam (0 diem). Bai con lam duoc, bai chua mo va bai da nop nhung chua cong bo diem KHONG tinh.
/// </summary>
public class StudentTranscriptBuilderTests
{
    private static int _id;

    /// <summary>score/maxScore la thang cua de (vi du 90/100); NULL la khong co diem de doc.</summary>
    private static (StudentExamItemDto Item, string? Subject) Exam(
        string subject, string status, decimal? score = null, decimal? maxScore = null, string lifecycle = "open", string group = "Lop A")
    {
        var id = ++_id;

        return (new StudentExamItemDto
        {
            AssignmentId = id,
            ExamId = id,
            Title = $"Bai {id}",
            Description = string.Empty,
            Topic = string.Empty,
            DurationMinutes = 30,
            TotalQuestions = 9,
            TotalScore = 10,
            ShuffleQuestions = false,
            ShuffleAnswers = false,
            OpenAt = DateTime.UtcNow.AddDays(-5),
            CloseAt = DateTime.UtcNow.AddDays(5),
            TeacherName = "GV",
            Lifecycle = lifecycle,
            Status = status,
            CanStart = false,
            ShowResultAfterSubmit = score is not null,
            AssignedAt = DateTime.UtcNow.AddDays(-6),
            Score = score,
            MaxScore = maxScore,
            GroupId = 1,
            GroupName = group
        }, subject);
    }

    [Fact]
    public void Diem_trung_binh_mon_la_trung_binh_cac_bai_da_cham_quy_ve_thang_10()
    {
        var transcript = StudentTranscriptBuilder.Build([
            Exam("Toan", "graded", 90, 100),   // 9
            Exam("Toan", "graded", 7, 10),     // 7: de cham thang khac van quy ve thang 10
        ]);

        var subject = transcript.Subjects.Should().ContainSingle().Subject;
        subject.Average.Should().Be(8m);
        subject.CountedExams.Should().Be(2);
        transcript.OverallAverage.Should().Be(8m);
    }

    [Fact]
    public void Bai_het_han_ma_em_khong_lam_tinh_0_diem()
    {
        var transcript = StudentTranscriptBuilder.Build([
            Exam("Toan", "graded", 100, 100),
            Exam("Toan", "expired", lifecycle: "closed"),
        ]);

        var subject = transcript.Subjects.Single();
        subject.Average.Should().Be(5m);
        subject.CountedExams.Should().Be(2);
        subject.MissedExams.Should().Be(1);
        subject.Exams.Single(exam => exam.Status == "expired").Counted.Should().BeTrue();
        subject.Exams.Single(exam => exam.Status == "expired").Score.Should().BeNull("bai bo lo khong co diem de hien, chi duoc tinh 0 vao trung binh");
    }

    [Fact]
    public void Bai_con_lam_duoc_bai_sap_toi_va_bai_chua_cong_bo_diem_khong_tinh()
    {
        var transcript = StudentTranscriptBuilder.Build([
            Exam("Toan", "graded", 80, 100),                                  // 8, tinh
            Exam("Toan", "assigned", lifecycle: "open"),                       // con lam duoc: chua tinh
            Exam("Toan", "inprogress", lifecycle: "open"),                     // dang lam do: chua tinh
            Exam("Toan", "assigned", lifecycle: "scheduled"),                  // sap toi: chua tinh
            Exam("Toan", "graded", score: null, maxScore: null),               // da nop, giao vien chua cho xem diem: khong co so de tinh
        ]);

        var subject = transcript.Subjects.Single();
        subject.Average.Should().Be(8m);
        subject.CountedExams.Should().Be(1);
        subject.TotalExams.Should().Be(5);
        subject.PendingExams.Should().Be(2);
        subject.UpcomingExams.Should().Be(1);
        subject.HiddenExams.Should().Be(1);
        subject.Exams.Count(exam => exam.Counted).Should().Be(1);
    }

    [Fact]
    public void Mon_chua_co_bai_nao_den_han_tinh_thi_chua_co_diem_trung_binh()
    {
        var transcript = StudentTranscriptBuilder.Build([
            Exam("Toan", "assigned", lifecycle: "open"),
            Exam("Toan", "graded", score: null, maxScore: null),
        ]);

        transcript.Subjects.Single().Average.Should().BeNull();
        transcript.OverallAverage.Should().BeNull();
    }

    [Fact]
    public void Diem_chung_la_trung_binh_cac_diem_mon_va_bo_qua_mon_chua_co_diem()
    {
        var transcript = StudentTranscriptBuilder.Build([
            Exam("Toan", "graded", 100, 100),                 // mon Toan 10
            Exam("Toan", "graded", 100, 100),
            Exam("Van", "graded", 60, 100),                   // mon Van 6
            Exam("Anh", "assigned", lifecycle: "open"),       // mon Anh chua co diem: khong keo trung binh
        ]);

        transcript.Subjects.Should().HaveCount(3);
        transcript.OverallAverage.Should().Be(8m);
    }

    [Fact]
    public void Mon_xep_theo_ten_va_mon_chua_gan_xuong_cuoi()
    {
        var transcript = StudentTranscriptBuilder.Build([
            Exam("Toan", "graded", 80, 100),
            Exam(null!, "graded", 80, 100),
            Exam("Anh van", "graded", 80, 100),
        ]);

        transcript.Subjects.Select(subject => subject.SubjectName)
            .Should().Equal("Anh van", "Toan", StudentTranscriptBuilder.UnassignedSubjectName);
    }

    [Fact]
    public void Ten_lop_cua_mon_khong_trung_lap_va_bo_qua_bai_giao_le()
    {
        var transcript = StudentTranscriptBuilder.Build([
            Exam("Toan", "graded", 80, 100, group: "Lop B"),
            Exam("Toan", "graded", 80, 100, group: "Lop A"),
            Exam("Toan", "graded", 80, 100, group: "Lop A"),
        ]);

        transcript.Subjects.Single().GroupNames.Should().Equal("Lop A", "Lop B");
    }

    [Fact]
    public void Khong_co_bai_nao_thi_bang_diem_rong()
    {
        var transcript = StudentTranscriptBuilder.Build([]);

        transcript.Subjects.Should().BeEmpty();
        transcript.OverallAverage.Should().BeNull();
    }
}
