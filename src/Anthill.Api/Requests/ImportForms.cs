namespace Anthill.Api.Requests;

/// <summary>
/// Thân multipart của <c>POST /api/imports/students</c>. Nằm ở tầng Api vì chứa <see cref="IFormFile"/>: tầng Application
/// không được biết gì về ASP.NET Core, nên controller bóc file thành <see cref="Stream"/> trước khi gọi service.
/// </summary>
public sealed class ImportStudentsForm
{
    /// <summary>File .xlsx.</summary>
    public IFormFile? File { get; init; }

    /// <summary>Lớp áp cho những dòng để trống ô Lớp.</summary>
    public int? GroupId { get; init; }

    /// <summary>Mật khẩu ban đầu dùng chung cho các tài khoản mới của đợt này.</summary>
    public string? Password { get; init; }

    /// <summary><c>true</c>: chỉ kiểm tra, chưa ghi gì.</summary>
    public bool DryRun { get; init; }
}

/// <summary>Thân multipart của <c>POST /api/imports/teachers</c>.</summary>
public sealed class ImportTeachersForm
{
    public IFormFile? File { get; init; }

    /// <summary>Môn áp cho những dòng để trống ô Môn.</summary>
    public int? SubjectId { get; init; }

    public string? Password { get; init; }

    public bool DryRun { get; init; }
}

/// <summary>Thân multipart của <c>POST /api/imports/school</c> (nhập tổng hợp: giáo viên, lớp và học sinh từ một file).</summary>
public sealed class ImportSchoolForm
{
    public IFormFile? File { get; init; }

    /// <summary>Môn áp cho những giáo viên chủ nhiệm mà file không ghi môn.</summary>
    public int? SubjectId { get; init; }

    /// <summary>Mật khẩu ban đầu dùng chung cho mọi tài khoản mới của đợt (giáo viên lẫn học sinh).</summary>
    public string? Password { get; init; }

    /// <summary><c>true</c>: công bố ngay các lớp vừa tạo, để giáo viên thấy.</summary>
    public bool PublishGroups { get; init; }

    public bool DryRun { get; init; }
}

/// <summary>Thân multipart của <c>POST /api/imports/groups</c>.</summary>
public sealed class ImportGroupsForm
{
    public IFormFile? File { get; init; }

    public bool DryRun { get; init; }
}
