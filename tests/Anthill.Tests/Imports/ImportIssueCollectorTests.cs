using Anthill.Application.Common;
using FluentAssertions;

namespace Anthill.Tests.Imports;

/// <summary>
/// Gom cac dong bi tu choi: so dong hong duoc dem DU, chi danh sach tra ve bi cat. File hong ca nghin dong khong duoc phinh
/// phan hoi, nhung nguoi dung van phai biet con so that.
/// </summary>
public class ImportIssueCollectorTests
{
    [Fact]
    public void Mot_dong_bi_ghi_nhieu_loi_chi_tinh_mot_dong_hong()
    {
        var collector = new ImportIssueCollector();

        collector.Reject(7, "Email", "Email sai.");
        collector.Reject(7, "So dien thoai", "So dien thoai sai.");

        collector.FailedRows.Should().Be(1);
        collector.IsRejected(7).Should().BeTrue();
        collector.IsRejected(8).Should().BeFalse();
        collector.Snapshot().Issues.Should().HaveCount(2);
    }

    [Fact]
    public void Loi_duoc_sap_theo_so_dong_va_ten_cot()
    {
        var collector = new ImportIssueCollector();

        collector.Reject(9, "Lop", "c");
        collector.Reject(3, "Ho va ten", "a");
        collector.Reject(9, "Email", "b");

        var (issues, truncated) = collector.Snapshot();

        truncated.Should().BeFalse();
        issues.Select(issue => (issue.Row, issue.Column)).Should().Equal((3, "Ho va ten"), (9, "Email"), (9, "Lop"));
    }

    [Fact]
    public void Danh_sach_bi_cat_o_nguong_nhung_so_dong_hong_van_dem_du()
    {
        var collector = new ImportIssueCollector();

        for (var row = 2; row < 2 + ImportIssueCollector.MaxIssues + 100; row++)
        {
            collector.Reject(row, "Ho va ten", "Chua co ho va ten.");
        }

        var (issues, truncated) = collector.Snapshot();

        collector.FailedRows.Should().Be(ImportIssueCollector.MaxIssues + 100);
        issues.Should().HaveCount(ImportIssueCollector.MaxIssues);
        truncated.Should().BeTrue();

        // Cắt từ cuối: các lỗi ở đầu file là thứ người dùng sửa trước.
        issues[0].Row.Should().Be(2);
    }

    [Fact]
    public void Dung_nguong_thi_khong_bi_coi_la_bi_cat()
    {
        var collector = new ImportIssueCollector();

        for (var row = 2; row < 2 + ImportIssueCollector.MaxIssues; row++)
        {
            collector.Reject(row, null, "Loi.");
        }

        collector.Snapshot().Truncated.Should().BeFalse();
    }
}
