using Anthill.Application.Common;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Attempts;

/// <summary>
/// <c>POST /api/exam-attempts</c> là idempotent (§4.7) — và đây là chỗ định nghĩa điều đó.
/// </summary>
/// <remarks>
/// Ba tình huống dẫn tới cùng một lời gọi thứ hai: học sinh bấm F5, đổi sang máy khác, hoặc trình
/// duyệt tự gửi lại sau khi request đầu timeout. Cả ba đều phải trả về đúng lượt cũ. Sai ở đây
/// nghĩa là hoặc sinh ra lượt thứ hai với bộ câu khác, hoặc cấp lại đồng hồ từ đầu — cách gian lận
/// đơn giản nhất mà một học sinh có thể nghĩ ra.
/// </remarks>
public class AttemptStartPolicyTests
{
    private static readonly DateTime Now = new(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Chua_co_luot_va_de_dang_mo_thi_tao_moi()
    {
        AttemptStartPolicy.Decide(ExamLifecycle.Open, attemptStatus: null, expiresAtUtc: null, Now)
            .Should().Be(AttemptStartDecision.CreateNew);
    }

    [Fact]
    public void Goi_lai_khi_dang_lam_do_thi_tra_ve_dung_luot_cu()
    {
        // Nhánh quan trọng nhất của cả file: không tạo lượt mới, không đụng vào đồng hồ.
        AttemptStartPolicy.Decide(
                ExamLifecycle.Open,
                AssignmentStatus.InProgress,
                Now.AddMinutes(20),
                Now)
            .Should().Be(AttemptStartDecision.Resume);
    }

    [Fact]
    public void Dang_lam_do_nhung_da_qua_han_thi_chot_so_chu_khong_cho_lam_tiep()
    {
        // Background job có thể chưa chạy tới. Trả về một phiên còn gõ được cho một lượt đã hết giờ
        // là mở lại cửa sổ ghi đã đóng.
        AttemptStartPolicy.Decide(
                ExamLifecycle.Open,
                AssignmentStatus.InProgress,
                Now.AddSeconds(-1),
                Now)
            .Should().Be(AttemptStartDecision.Finalise);
    }

    [Fact]
    public void Dung_giay_het_gio_van_duoc_lam_tiep()
    {
        AttemptStartPolicy.Decide(ExamLifecycle.Open, AssignmentStatus.InProgress, Now, Now)
            .Should().Be(AttemptStartDecision.Resume);
    }

    [Theory]
    [InlineData(AssignmentStatus.Submitted)]
    [InlineData(AssignmentStatus.Graded)]
    public void Da_nop_roi_thi_khong_lam_lai(AssignmentStatus status)
    {
        // §8: làm lại bài không nằm trong v1, mỗi assignment đúng một lượt.
        AttemptStartPolicy.Decide(ExamLifecycle.Open, status, Now.AddMinutes(-30), Now)
            .Should().Be(AttemptStartDecision.AlreadyFinished);
    }

    [Fact]
    public void Da_nop_roi_thi_van_bao_da_nop_du_de_dong_trong_luc_do()
    {
        // Thứ tự xét có chủ đích: lượt đã có luôn thắng vòng đời của đề. Báo "đề đã đóng" cho một
        // em vừa nộp xong khiến em tưởng bài mình không được ghi nhận.
        AttemptStartPolicy.Decide(ExamLifecycle.Closed, AssignmentStatus.Graded, Now.AddMinutes(-30), Now)
            .Should().Be(AttemptStartDecision.AlreadyFinished);
    }

    [Fact]
    public void Dang_lam_do_thi_giao_vien_doi_lich_khong_cat_ngang_duoc()
    {
        // Giáo viên dời giờ đóng về quá khứ giữa buổi thi: những em đang làm vẫn phải làm tiếp cho
        // hết hạn giờ đã chốt trong ExpiresAt của chính lượt đó.
        AttemptStartPolicy.Decide(ExamLifecycle.Closed, AssignmentStatus.InProgress, Now.AddMinutes(10), Now)
            .Should().Be(AttemptStartDecision.Resume);
    }

    [Theory]
    [InlineData(ExamLifecycle.Draft)]
    [InlineData(ExamLifecycle.Scheduled)]
    public void Chua_toi_gio_thi_chua_vao_duoc(ExamLifecycle lifecycle)
    {
        // Nháp cũng vào nhánh này: giáo viên rút một đề đã giao về nháp thì học sinh gặp đúng câu
        // "chưa mở" thay vì một lỗi kỹ thuật.
        AttemptStartPolicy.Decide(lifecycle, attemptStatus: null, expiresAtUtc: null, Now)
            .Should().Be(AttemptStartDecision.NotOpenYet);
    }

    [Fact]
    public void De_dong_ma_chua_tung_bat_dau_thi_het_co_hoi()
    {
        AttemptStartPolicy.Decide(ExamLifecycle.Closed, attemptStatus: null, expiresAtUtc: null, Now)
            .Should().Be(AttemptStartDecision.Closed);
    }

    [Fact]
    public void Luot_da_bi_danh_dau_het_han_thi_khong_mo_lai()
    {
        AttemptStartPolicy.Decide(ExamLifecycle.Open, AssignmentStatus.Expired, Now.AddMinutes(-5), Now)
            .Should().Be(AttemptStartDecision.Closed);
    }

    [Fact]
    public void Luot_dang_lam_do_ma_thieu_han_gio_thi_chot_so_chu_khong_cho_lam_vo_thoi_han()
    {
        // Dữ liệu hỏng — một lượt InProgress không có hạn giờ — phải rơi về phía an toàn. Coi là
        // còn hạn nghĩa là cấp cho nó một buổi thi không bao giờ kết thúc.
        AttemptStartPolicy.Decide(ExamLifecycle.Open, AssignmentStatus.InProgress, expiresAtUtc: null, Now)
            .Should().Be(AttemptStartDecision.Finalise);
    }
}
