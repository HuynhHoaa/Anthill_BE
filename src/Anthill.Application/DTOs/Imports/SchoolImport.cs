namespace Anthill.Application.DTOs.Imports;

/// <summary>Lựa chọn của quản trị viên khi nhập tổng hợp (giáo viên, lớp và học sinh từ một file).</summary>
/// <param name="DefaultSubjectId">Môn áp cho những giáo viên chủ nhiệm mà file không ghi môn.</param>
/// <param name="Password">Mật khẩu ban đầu dùng chung cho mọi tài khoản mới của đợt (giáo viên lẫn học sinh).</param>
/// <param name="PublishGroups">Công bố ngay các lớp vừa tạo để giáo viên thấy; mặc định lớp mới ở dạng nháp.</param>
/// <param name="DryRun"><c>true</c>: chỉ kiểm tra và đếm, chưa ghi gì.</param>
public sealed record ImportSchoolOptions(int? DefaultSubjectId, string? Password, bool PublishGroups, bool DryRun);

/// <summary>Một học sinh trong danh sách xem trước.</summary>
/// <param name="Row">Số dòng trong file Excel.</param>
/// <param name="Exists">Đã có tài khoản trong hệ thống (nhập sẽ chỉ xếp thêm vào lớp, không tạo mới hay sửa hồ sơ).</param>
public sealed record SchoolPreviewStudentDto(int Row, string Code, string FullName, bool Exists);

/// <summary>
/// Một lớp trong danh sách xem trước: lớp, giáo viên chủ nhiệm, môn và các học sinh sẽ vào lớp. Chỉ gồm những gì nhập được; dòng hỏng
/// nằm ở danh sách lỗi.
/// </summary>
/// <param name="Exists">Lớp đã có (nhập chỉ xếp thêm học sinh vào), không phải lớp mới.</param>
/// <param name="TeacherExists">Giáo viên đã có trong hệ thống, không phải người mới.</param>
/// <param name="Subject">Môn của giáo viên (với người đã có là môn trong hồ sơ).</param>
public sealed record SchoolPreviewGroupDto(
    string Name,
    bool Exists,
    string TeacherName,
    string TeacherEmail,
    bool TeacherExists,
    string Subject,
    IReadOnlyList<SchoolPreviewStudentDto> Students,
    string? TermName = null);

/// <summary>
/// Kết quả nhập tổng hợp: một file, ba nơi quản lý. Mỗi con số đếm theo từng loại bản ghi, còn <see cref="Failed"/> đếm theo
/// <b>dòng</b> của file.
/// </summary>
public sealed record SchoolImportResultDto
{
    /// <summary><c>true</c>: chỉ kiểm tra, chưa ghi gì.</summary>
    public required bool DryRun { get; init; }

    /// <summary>Số dòng dữ liệu đọc được (không tính dòng tiêu đề và dòng trống).</summary>
    public required int TotalRows { get; init; }

    /// <summary>
    /// Tên các môn mới được tạo vì file ghi một môn mà hệ thống chưa có (chỉ tính môn có giáo viên mới dùng). Xem trước hiện danh sách này
    /// để người dùng bắt lỗi gõ sai trước khi nhập thật.
    /// </summary>
    public required IReadOnlyList<string> SubjectsCreated { get; init; }

    /// <summary>
    /// Danh sách để quản trị viên soát trước khi nhập: từng lớp kèm giáo viên, môn và học sinh. Chỉ có khi nhập thử; nhập thật trả
    /// danh sách rỗng (kết quả nhập thật là các con số).
    /// </summary>
    public required IReadOnlyList<SchoolPreviewGroupDto> Preview { get; init; }

    /// <summary>
    /// Tên các kỳ học mới được tạo vì file ghi kỳ mà hệ thống chưa có (xem trước chỉ liệt kê). Giống <see cref="SubjectsCreated"/>: file là
    /// nguồn, nên không bắt người dùng tạo kỳ trước.
    /// </summary>
    public required IReadOnlyList<string> TermsCreated { get; init; }

    public required int TeachersCreated { get; init; }

    /// <summary>Giáo viên đã có sẵn trong hệ thống (cùng email và họ tên).</summary>
    public required int TeachersUnchanged { get; init; }

    public required int GroupsCreated { get; init; }

    /// <summary>Lớp đã có sẵn với đúng giáo viên chủ nhiệm đó.</summary>
    public required int GroupsUnchanged { get; init; }

    /// <summary>Số lớp được công bố ngay trong lần nhập này (chỉ khi người dùng chọn công bố).</summary>
    public required int GroupsPublished { get; init; }

    public required int StudentsCreated { get; init; }

    /// <summary>Học sinh đã có tài khoản, nay được xếp thêm vào lớp ghi trên dòng.</summary>
    public required int StudentsPlaced { get; init; }

    public required int StudentsUnchanged { get; init; }

    /// <summary>Số dòng của file bị từ chối — đếm đủ, kể cả khi <see cref="Issues"/> bị cắt.</summary>
    public required int Failed { get; init; }

    public required IReadOnlyList<ImportIssueDto> Issues { get; init; }

    public required bool IssuesTruncated { get; init; }

    /// <summary>Các tài khoản vừa tạo (giáo viên và học sinh), chỉ khi nhập thật.</summary>
    public required IReadOnlyList<ImportedAccountDto> Accounts { get; init; }
}
