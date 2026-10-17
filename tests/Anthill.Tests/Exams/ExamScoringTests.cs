using Anthill.Application.Common;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Exams;

/// <summary>
/// Tổng điểm của một đề: số câu mỗi mức (từ ma trận) nhân điểm mức đó. 10 là tổng chuẩn, không bắt buộc.
/// </summary>
public class ExamScoringTests
{
    [Fact]
    public void Tong_diem_la_so_cau_moi_muc_nhan_diem_muc_do()
    {
        // 40 câu, 40/40/20 → 16 Dễ, 16 Trung bình, 8 Khó.
        var total = ExamScoring.TotalOf(40, 40, 40, pointEasy: 0.2m, pointMedium: 0.25m, pointHard: 0.35m);

        total.Should().Be(16 * 0.2m + 16 * 0.25m + 8 * 0.35m);
        total.Should().Be(10m);
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
    [InlineData(9, 10, 9)]
    [InlineData(60, 100, 6)]
    [InlineData(47.5, 50, 9.5)]
    public void Quy_ve_thang_10_chia_cho_diem_toi_da_cua_chinh_de(double score, double maxScore, double expected)
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
        // Em được 9/10 ở một đề và 60/100 ở đề cũ chấm trên 100: trung bình thô là 34,5, trung bình đúng là 7,5.
        var scores = new[] { (9m, 10m), (60m, 100m) }
            .Select(pair => ExamScoring.ToStandardScale(pair.Item1, pair.Item2)!.Value)
            .ToList();

        scores.Average().Should().Be(7.5m);
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
            PointEasy = 0.4m,
            PointMedium = 0.5m,
            PointHard = 0.75m
        };

        // 10 Dễ, 6 Trung bình, 4 Khó: 4 + 3 + 3.
        ExamScoring.TotalOf(exam).Should().Be(10m);
    }

    [Fact]
    public void Tong_khong_phai_10_van_la_con_so_that()
    {
        // 56 câu 30/50/20 → 17 Dễ, 28 Trung bình, 11 Khó. Điểm 0,15 / 0,15 / 0,3 ra 10,05: server giữ
        // đúng con số đó để giao diện hiện "10.05 / 10" và hỏi xác nhận, không làm tròn về 10.
        var total = ExamScoring.TotalOf(56, 30, 50, pointEasy: 0.15m, pointMedium: 0.15m, pointHard: 0.3m);

        total.Should().Be(17 * 0.15m + 28 * 0.15m + 11 * 0.3m);
        total.Should().Be(10.05m);
        total.Should().NotBe(ExamScoring.TotalScore);
    }

    [Fact]
    public void Lech_mot_phan_tram_khong_bi_lam_tron_mat()
    {
        // 3 câu × 3.33 = 9.99: cách "chia đều" quen tay mà ai cũng làm. decimal giữ đúng 9.99 chứ
        // không để nó trôi thành 10.
        var total = ExamScoring.TotalOf(3, 34, 33, pointEasy: 3.33m, pointMedium: 3.33m, pointHard: 3.33m);

        total.Should().Be(9.99m);
    }
}
