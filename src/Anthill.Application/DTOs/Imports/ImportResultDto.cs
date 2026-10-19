namespace Anthill.Application.DTOs.Imports;

/// <summary>Một dòng của file Excel bị từ chối, và vì sao.</summary>
public sealed record ImportIssueDto
{
    /// <summary>Số dòng trong file Excel (dòng tiêu đề là 1).</summary>
    public required int Row { get; init; }

    /// <summary>Tên cột có vấn đề; vắng mặt khi lỗi nằm ở cả dòng (ví dụ trùng một dòng khác).</summary>
    public string? Column { get; init; }

    public required string Message { get; init; }
}

/// <summary>Một tài khoản vừa được tạo — đủ để in phiếu tài khoản phát cho người dùng.</summary>
public sealed record ImportedAccountDto
{
    public required string FullName { get; init; }

    /// <summary>Tên đăng nhập (với giáo viên là email).</summary>
    public required string Username { get; init; }

    /// <summary>Mã học sinh; vắng mặt với giáo viên.</summary>
    public string? Code { get; init; }

    /// <summary>Tên lớp (học sinh) hoặc tên môn (giáo viên) nếu có.</summary>
    public string? Group { get; init; }
}

/// <summary>
/// Kết quả một lần nhập từ Excel — cả lần kiểm tra thử (<see cref="DryRun"/>) lẫn lần nhập thật.
/// </summary>
/// <remarks>
/// Nhập hàng nghìn dòng thì một dòng hỏng không được làm hỏng cả mẻ, nhưng cũng không được âm thầm bỏ qua: dòng hợp lệ được
/// nhập, dòng hỏng được liệt kê kèm số dòng và lý do để sửa rồi nhập lại. File sửa xong có thể gửi lại nguyên cả file — dòng
/// đã nhập rồi được nhận ra (<see cref="Unchanged"/>) chứ không báo trùng.
/// </remarks>
public sealed record ImportResultDto
{
    /// <summary><c>true</c>: chỉ kiểm tra, chưa ghi gì.</summary>
    public required bool DryRun { get; init; }

    /// <summary>Số dòng dữ liệu đọc được trong file (không tính dòng tiêu đề và dòng trống).</summary>
    public required int TotalRows { get; init; }

    /// <summary>Số bản ghi mới được tạo (hoặc sẽ được tạo, khi <see cref="DryRun"/>).</summary>
    public required int Created { get; init; }

    /// <summary>Học sinh đã có tài khoản, nay được xếp thêm vào một lớp em chưa học.</summary>
    public required int Placed { get; init; }

    /// <summary>Dòng khớp với bản ghi đã có và không còn gì để làm (đã ở đúng lớp, đã có giáo viên đó...).</summary>
    public required int Unchanged { get; init; }

    /// <summary>Số dòng bị từ chối.</summary>
    public required int Failed { get; init; }

    /// <summary>Các dòng bị từ chối, theo thứ tự dòng. Bị cắt ở một ngưỡng — xem <see cref="IssuesTruncated"/>.</summary>
    public required IReadOnlyList<ImportIssueDto> Issues { get; init; }

    /// <summary><c>true</c> khi còn nhiều dòng hỏng hơn số trả về ở <see cref="Issues"/>.</summary>
    public required bool IssuesTruncated { get; init; }

    /// <summary>Các tài khoản vừa tạo (chỉ khi nhập thật).</summary>
    public required IReadOnlyList<ImportedAccountDto> Accounts { get; init; }

    /// <summary>Tên các kỳ học mới được tạo vì file ghi kỳ mà hệ thống chưa có (chỉ nhập lớp; xem trước chỉ liệt kê).</summary>
    public IReadOnlyList<string> TermsCreated { get; init; } = [];
}
