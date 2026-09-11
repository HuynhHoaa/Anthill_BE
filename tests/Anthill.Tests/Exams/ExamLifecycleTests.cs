using Anthill.Application.Mappings;
using Anthill.Domain.Entities;
using Anthill.Domain.Enums;
using FluentAssertions;

namespace Anthill.Tests.Exams;

/// <summary>
/// Vòng đời của đề là dữ liệu <b>dẫn xuất</b>: chỉ <c>IsDraft</c> nằm trong database, ba trạng
/// thái còn lại tính từ hai mốc thời gian so với giờ server lúc đọc (§5).
/// </summary>
/// <remarks>
/// Điều đáng kiểm ở đây là <b>hai đầu mút</b>. Một đề đúng vào giây mở phải là "đang mở" chứ không
/// còn "sắp diễn ra", và đúng vào giây đóng vẫn phải là "đang mở" chứ chưa "đã đóng" — học sinh
/// bấm Nộp ở giây cuối cùng thì bài phải được nhận. Sai một dấu <c>&lt;</c> thành <c>&lt;=</c> ở
/// đây không làm hỏng test nào khác, nhưng làm mất bài của học sinh.
/// </remarks>
public class ExamLifecycleTests
{
    private static readonly DateTime OpenAt = new(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CloseAt = new(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);

    private static Exam Exam(bool isDraft = false) => new()
    {
        Title = "Kiểm tra giữa kỳ",
        OpenAt = OpenAt,
        CloseAt = CloseAt,
        IsDraft = isDraft
    };

    [Fact]
    public void Ban_nhap_luon_la_draft_bat_ke_lich_the_nao()
    {
        // Giữa cửa sổ mở, nhưng vẫn là nháp: cờ IsDraft phải thắng phép so thời gian, nếu không
        // một bản nháp lưu với lịch quá khứ sẽ tự hiện ra là "đang mở" cho học sinh.
        var lifecycle = Exam(isDraft: true).ResolveLifecycle(OpenAt.AddMinutes(30));

        lifecycle.Should().Be(ExamLifecycle.Draft);
    }

    [Fact]
    public void Truoc_gio_mo_la_scheduled()
    {
        Exam().ResolveLifecycle(OpenAt.AddSeconds(-1)).Should().Be(ExamLifecycle.Scheduled);
    }

    [Fact]
    public void Dung_giay_mo_da_la_open()
    {
        Exam().ResolveLifecycle(OpenAt).Should().Be(ExamLifecycle.Open);
    }

    [Fact]
    public void Giua_cua_so_la_open()
    {
        Exam().ResolveLifecycle(OpenAt.AddMinutes(30)).Should().Be(ExamLifecycle.Open);
    }

    [Fact]
    public void Dung_giay_dong_van_con_open()
    {
        // Học sinh bấm Nộp ở giây cuối cùng thì bài vẫn phải được nhận.
        Exam().ResolveLifecycle(CloseAt).Should().Be(ExamLifecycle.Open);
    }

    [Fact]
    public void Sau_gio_dong_la_closed()
    {
        Exam().ResolveLifecycle(CloseAt.AddSeconds(1)).Should().Be(ExamLifecycle.Closed);
    }

    [Fact]
    public void Moc_thoi_gian_khong_ghi_Kind_van_duoc_hieu_la_UTC()
    {
        // EF Core đọc cột timestamptz ra DateTime có Kind = Unspecified ở vài đường đi. Nếu phép so
        // không chốt Kind trước thì kết quả lệch đúng bằng chênh lệch múi giờ của máy chủ — trên
        // máy giờ Việt Nam là bảy tiếng, đủ để một đề đã đóng vẫn hiện ra là đang mở.
        var exam = new Exam
        {
            Title = "Kiểm tra giữa kỳ",
            OpenAt = DateTime.SpecifyKind(OpenAt, DateTimeKind.Unspecified),
            CloseAt = DateTime.SpecifyKind(CloseAt, DateTimeKind.Unspecified),
            IsDraft = false
        };

        exam.ResolveLifecycle(OpenAt.AddMinutes(30)).Should().Be(ExamLifecycle.Open);
        exam.ResolveLifecycle(CloseAt.AddHours(3)).Should().Be(ExamLifecycle.Closed);
    }

    [Theory]
    [InlineData(ExamLifecycle.Draft, "draft")]
    [InlineData(ExamLifecycle.Scheduled, "scheduled")]
    [InlineData(ExamLifecycle.Open, "open")]
    [InlineData(ExamLifecycle.Closed, "closed")]
    public void Chuoi_vong_doi_viet_thuong_dung_nhu_frontend_khai_bao(
        ExamLifecycle lifecycle,
        string expected)
    {
        lifecycle.ToLifecycleString().Should().Be(expected);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("all", null)]
    [InlineData("ALL", null)]
    [InlineData("draft", ExamLifecycle.Draft)]
    [InlineData("OPEN", ExamLifecycle.Open)]
    public void Bo_loc_vong_doi_nhan_gia_tri_hop_le_va_coi_all_la_khong_loc(
        string? input,
        ExamLifecycle? expected)
    {
        ExamMappingExtensions.TryParseLifecycleFilter(input, out var parsed).Should().BeTrue();
        parsed.Should().Be(expected);
    }

    [Theory]
    [InlineData("published")]
    [InlineData("dang-mo")]
    [InlineData("42")]
    public void Bo_loc_vong_doi_tu_choi_gia_tri_la_thay_vi_am_tham_bo_loc(string input)
    {
        // Bỏ lọc trong im lặng nghĩa là trả về nhiều hơn người dùng yêu cầu mà vẫn kèm mã 200 —
        // kiểu sai không ai phát hiện.
        ExamMappingExtensions.TryParseLifecycleFilter(input, out var parsed).Should().BeFalse();
        parsed.Should().BeNull();
    }
}
