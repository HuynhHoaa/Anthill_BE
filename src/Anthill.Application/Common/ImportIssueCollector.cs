using Anthill.Application.DTOs.Imports;

namespace Anthill.Application.Common;

/// <summary>
/// Gom các dòng bị từ chối trong một lần nhập Excel — một chỗ cho cả ba bộ nhập (học sinh, giáo viên, lớp), để cách đếm và cắt
/// danh sách không lệch nhau.
/// </summary>
/// <remarks>
/// Số dòng hỏng được đếm <b>đủ</b>; chỉ danh sách trả về bị cắt ở <see cref="MaxIssues"/>, để file hỏng cả nghìn dòng không
/// phình phản hồi mà người dùng vẫn biết con số thật.
/// </remarks>
public sealed class ImportIssueCollector
{
    /// <summary>Số lỗi tối đa trả về trong một phản hồi.</summary>
    public const int MaxIssues = 500;

    private readonly List<ImportIssueDto> _issues = [];
    private readonly HashSet<int> _failedRows = [];

    /// <summary>Số dòng bị từ chối (một dòng chỉ tính một lần dù bị ghi nhiều lỗi).</summary>
    public int FailedRows => _failedRows.Count;

    public bool IsRejected(int row) => _failedRows.Contains(row);

    /// <summary>Từ chối một dòng. <paramref name="column"/> NULL khi lỗi nằm ở cả dòng.</summary>
    public void Reject(int row, string? column, string message)
    {
        _failedRows.Add(row);
        _issues.Add(new ImportIssueDto { Row = row, Column = column, Message = message });
    }

    /// <summary>Các lỗi theo thứ tự dòng, đã cắt ở ngưỡng, và có bị cắt hay không.</summary>
    public (IReadOnlyList<ImportIssueDto> Issues, bool Truncated) Snapshot() =>
    (
        _issues
            .OrderBy(issue => issue.Row)
            .ThenBy(issue => issue.Column, StringComparer.Ordinal)
            .Take(MaxIssues)
            .ToList(),
        _issues.Count > MaxIssues
    );
}
