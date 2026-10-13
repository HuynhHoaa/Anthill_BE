using Anthill.Application.Common;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Exams;

/// <summary>
/// Tổng điểm của một đề: số câu mỗi mức (từ ma trận) nhân điểm mức đó. 100 là tổng chuẩn, không bắt buộc.
/// </summary>
public class ExamScoringTests
{
    [Fact]
    public void Tong_diem_la_so_cau_moi_muc_nhan_diem_muc_do()
    {
        // 40 câu, 40/40/20 → 16 Dễ, 16 Trung bình, 8 Khó.
        var total = ExamScoring.TotalOf(40, 40, 40, pointEasy: 2m, pointMedium: 2.5m, pointHard: 3.5m);

        total.Should().Be(16 * 2m + 16 * 2.5m + 8 * 3.5m);
        total.Should().Be(100m);
    }

    [Fact]
    public void Muc_Kho_nhan_phan_du_nen_tong_so_cau_luon_dung()
    {
        // 10 câu, 33/33/34: Dễ 3, Trung bình 3, Khó 4 (phần dư), không phải 3 + 3 + 3. Ba điểm khác nhau
        // để chứng minh đúng 4 câu Khó được tính: 3×1 + 3×2 + 4×3 = 21 (chỉ 3 câu Khó thì ra 18).
        var total = ExamScoring.TotalOf(10, 33, 33, pointEasy: 1m, pointMedium: 2m, pointHard: 3m);

        total.Should().Be(21m);
    }

    [Fact]
    public void Diem_cua_mot_cau_lay_dung_theo_muc_do()
    {
        // Ba điểm khác nhau: đảo nhầm hai mức (Dễ với Trung bình) sẽ lộ ra ngay ở đây.
        var exam = new Exam { Title = "x", PointEasy = 1m, PointMedium = 2m, PointHard = 3m };

        exam.PointFor(QuestionDifficulty.Easy).Should().Be(1m);
        exam.PointFor(QuestionDifficulty.Medium).Should().Be(2m);
        exam.PointFor(QuestionDifficulty.Hard).Should().Be(3m);
    }

    [Theory]
    [InlineData(9, 10, 90)]
    [InlineData(60, 100, 60)]
    [InlineData(47.5, 50, 95)]
    public void Quy_ve_thang_100_chia_cho_diem_toi_da_cua_chinh_de(double score, double maxScore, double expected)
    {
        ExamScoring.ToStandardScale((decimal)score, (decimal)maxScore).Should().Be((decimal)expected);
    }

    [Fact]
    public void De_khong_co_diem_toi_da_thi_khong_quy_doi_duoc()
    {
        ExamScoring.ToStandardScale(5m, 0m).Should().BeNull();
    }

    [Fact]
    public void Trung_binh_cua_de_cu_va_de_moi_khong_cong_tho()
    {
        // Em được 9/10 ở đề cũ và 60/100 ở đề mới: trung bình thô là 34,5, trung bình đúng là 75.
        var scores = new[] { (9m, 10m), (60m, 100m) }
            .Select(pair => ExamScoring.ToStandardScale(pair.Item1, pair.Item2)!.Value)
            .ToList();

        scores.Average().Should().Be(75m);
    }

    [Fact]
    public void Do_tren_Exam_va_do_tren_request_cho_cung_mot_ket_qua()
    {
        var exam = new Exam
        {
            Title = "x",
            TotalQuestions = 20,
            EasyPercentage = 50,
            MediumPercentage = 30,
            HardPercentage = 20,
            PointEasy = 4m,
            PointMedium = 5m,
            PointHard = 7.5m
        };

        // 10 Dễ, 6 Trung bình, 4 Khó: 40 + 30 + 30.
        ExamScoring.TotalOf(exam).Should().Be(100m);
    }

    [Fact]
    public void Tong_khong_phai_100_van_la_con_so_that()
    {
        // 56 câu 30/50/20 → 17 Dễ, 28 Trung bình, 11 Khó. Điểm "đẹp" 1,5 / 1,5 / 3 ra 100,5: server giữ
        // đúng con số đó để giao diện hiện "100.5 / 100" và hỏi xác nhận, không làm tròn về 100.
        var total = ExamScoring.TotalOf(56, 30, 50, pointEasy: 1.5m, pointMedium: 1.5m, pointHard: 3m);

        total.Should().Be(17 * 1.5m + 28 * 1.5m + 11 * 3m);
        total.Should().Be(100.5m);
        total.Should().NotBe(ExamScoring.TotalScore);
    }

    [Fact]
    public void Lech_mot_phan_tram_khong_bi_lam_tron_mat()
    {
        // 3 câu × 33.33 = 99.99: cách "chia đều" quen tay mà ai cũng làm. decimal giữ đúng 99.99 chứ
        // không để nó trôi thành 100.
        var total = ExamScoring.TotalOf(3, 34, 33, pointEasy: 33.33m, pointMedium: 33.33m, pointHard: 33.33m);

        total.Should().Be(99.99m);
    }
}
