using Anthill.Application.Common;
using FluentAssertions;

namespace Anthill.Tests.Exams;

/// <summary>
/// H10 — một đề dùng bản đề chung (<c>RandomizePerStudent = false</c>) không được giao thêm người MỚI
/// một khi đã có em THẬT SỰ bắt đầu làm và cửa sổ hiệu lực của lượt đó đã đóng. Trước bản vá này,
/// <c>ExamGeneratorService</c> luôn phát lại đúng bản đề chung cho bất kỳ ai được giao thêm, bất kể đã
/// có ai thấy đáp án chưa — pin lại đây từng góc của bảng quyết định để lỗi đó không tái diễn.
/// </summary>
/// <remarks>
/// Bản đầu của bản vá này chỉ nhìn lịch (cửa sổ đã đóng hay chưa) mà quên mất "đã đóng" khác "đã có ai
/// vào thi": bộ kiểm HTTP <c>per-class-schedule.sh</c> giao lớp buổi sáng với khung giờ đặt sẵn ở quá
/// khứ (chưa từng ai vào thi) rồi giao tiếp lớp buổi chiều — một cách dùng per-class-schedule hoàn toàn
/// bình thường — và bản đầu đó chặn nhầm nó với lý do "đã lộ", trong khi chưa ai từng đọc được câu nào.
/// <see cref="Da_qua_moc_gio_nhung_chua_ai_bat_dau_thi_van_dung_lai"/> pin lại đúng ca hồi quy đó.
/// </remarks>
public class PaperReleasePolicyTests
{
    private static readonly DateTime Now = new(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Bat_randomize_thi_luon_dung_lai_du_moi_thu_khac_the_nao()
    {
        // Mỗi em một bản đề riêng — không có bản đề chung nào để phát lại, nên không có gì phải chặn.
        PaperReleasePolicy.DecideSharedPaperReuse(
                randomizePerStudent: true,
                sharedPaperExists: true,
                hasAnyStartedAttempt: true,
                priorReleaseAt: Now.AddDays(-1),
                asOfUtc: Now)
            .Should().Be(SharedPaperAssignmentDecision.Reuse);
    }

    [Fact]
    public void Chua_co_ban_de_chung_thi_dung_lai_sinh_moi_binh_thuong()
    {
        // Lần giao đầu tiên của một đề: chưa ai có bản đề nào để lộ.
        PaperReleasePolicy.DecideSharedPaperReuse(
                randomizePerStudent: false,
                sharedPaperExists: false,
                hasAnyStartedAttempt: false,
                priorReleaseAt: null,
                asOfUtc: Now)
            .Should().Be(SharedPaperAssignmentDecision.Reuse);
    }

    [Fact]
    public void Da_qua_moc_gio_nhung_chua_ai_bat_dau_thi_van_dung_lai()
    {
        // Ca hồi quy: lượt giao cũ đặt khung giờ đã trôi qua (releaseAt <= now), nhưng CHƯA từng ai bắt
        // đầu làm — ví dụ per-class-schedule.sh giao lớp buổi sáng với khung giờ ở quá khứ trước, rồi
        // giao tiếp lớp buổi chiều. Không ai từng đọc bản đề thì không có gì để lộ; cửa sổ đã đóng một
        // mình không đủ để chặn.
        PaperReleasePolicy.DecideSharedPaperReuse(
                randomizePerStudent: false,
                sharedPaperExists: true,
                hasAnyStartedAttempt: false,
                priorReleaseAt: Now.AddHours(-2),
                asOfUtc: Now)
            .Should().Be(SharedPaperAssignmentDecision.Reuse);
    }

    [Fact]
    public void Da_giao_nhung_chua_ai_thay_dap_an_thi_dung_lai_binh_thuong()
    {
        // Có em đã bắt đầu, nhưng mốc mở đáp án còn ở tương lai (đang làm dở, hoặc cửa sổ chưa đóng) —
        // lớp mới vào trước khi ai kịp thấy đáp án, đúng ý "cả lớp chung một đề".
        PaperReleasePolicy.DecideSharedPaperReuse(
                randomizePerStudent: false,
                sharedPaperExists: true,
                hasAnyStartedAttempt: true,
                priorReleaseAt: Now.AddHours(2),
                asOfUtc: Now)
            .Should().Be(SharedPaperAssignmentDecision.Reuse);
    }

    [Fact]
    public void Dung_bang_moc_mo_dap_an_thi_van_bi_coi_la_da_lo()
    {
        // Biên hợp lệ: đúng bằng mốc thì đáp án đã có thể rời server (ResultService dùng <=), nên phải
        // chặn chứ không đợi qua mốc mới chặn.
        PaperReleasePolicy.DecideSharedPaperReuse(
                randomizePerStudent: false,
                sharedPaperExists: true,
                hasAnyStartedAttempt: true,
                priorReleaseAt: Now,
                asOfUtc: Now)
            .Should().Be(SharedPaperAssignmentDecision.RejectAlreadyReleased);
    }

    [Fact]
    public void Da_co_em_bat_dau_va_da_qua_moc_mo_dap_an_thi_tu_choi_giao_them()
    {
        // Ca H10 thật: có em đã làm, cửa sổ của lớp cũ đã đóng từ trước — đáp án đã tới tay em đó. Giao
        // thêm lớp mới vào chính đề này sẽ phát lại đúng bản đề đã lộ.
        PaperReleasePolicy.DecideSharedPaperReuse(
                randomizePerStudent: false,
                sharedPaperExists: true,
                hasAnyStartedAttempt: true,
                priorReleaseAt: Now.AddHours(-1),
                asOfUtc: Now)
            .Should().Be(SharedPaperAssignmentDecision.RejectAlreadyReleased);
    }

    [Fact]
    public void Chua_giao_ai_bao_gio_thi_khong_the_bi_tu_choi()
    {
        // priorReleaseAt = NULL nghĩa là đề chưa từng giao cho ai — dù hai cờ kia lỡ là true (đáng lẽ
        // không xảy ra cùng lúc với priorReleaseAt = NULL), vẫn không có mốc nào để so nên không chặn.
        PaperReleasePolicy.DecideSharedPaperReuse(
                randomizePerStudent: false,
                sharedPaperExists: true,
                hasAnyStartedAttempt: true,
                priorReleaseAt: null,
                asOfUtc: Now)
            .Should().Be(SharedPaperAssignmentDecision.Reuse);
    }
}
