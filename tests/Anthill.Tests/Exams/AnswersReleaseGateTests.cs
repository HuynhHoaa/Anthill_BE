using Anthill.Application.Common;
using FluentAssertions;

namespace Anthill.Tests.Exams;

public class AnswersReleaseGateTests
{
    private static readonly DateTime Close = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void De_chua_giao_cho_lop_nao_thi_chua_co_gi_de_mo()
    {
        AnswersReleaseGate.Evaluate(null, Close).Should().Be(AnswersReleaseGateState.NoAssignments);
    }

    [Fact]
    public void Truoc_gio_dong_cua_lop_cuoi_thi_khoa()
    {
        AnswersReleaseGate.Evaluate(Close, Close.AddMinutes(-1)).Should().Be(AnswersReleaseGateState.StillRunning);
    }

    [Fact]
    public void Dung_giay_dong_bai_thi_lop_do_van_dang_mo()
    {
        // Cùng quy ước với ResolveAnswerVisibility: so bằng ">" nên đúng giây đóng chưa tính là hết giờ.
        AnswersReleaseGate.Evaluate(Close, Close).Should().Be(AnswersReleaseGateState.StillRunning);
    }

    [Fact]
    public void Qua_gio_dong_cua_lop_cuoi_thi_mo_duoc()
    {
        AnswersReleaseGate.Evaluate(Close, Close.AddSeconds(1)).Should().Be(AnswersReleaseGateState.TimeIsUp);
    }

    [Fact]
    public void Gio_khong_ghi_mui_gio_duoc_hieu_la_utc()
    {
        var unspecified = DateTime.SpecifyKind(Close, DateTimeKind.Unspecified);

        AnswersReleaseGate.Evaluate(unspecified, Close.AddMinutes(5)).Should().Be(AnswersReleaseGateState.TimeIsUp);
    }
}
