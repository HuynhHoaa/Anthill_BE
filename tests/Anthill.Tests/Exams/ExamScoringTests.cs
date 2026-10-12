using Anthill.Application.Common;
using Anthill.Domain.Entities;
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
        // 10 câu, 33/33/34: Dễ 3, Trung bình 3, Khó 4 (phần dư), không phải 3 + 3 + 3.
        var total = ExamScoring.TotalOf(10, 33, 33, pointEasy: 10m, pointMedium: 10m, pointHard: 10m);

        total.Should().Be(100m);
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
