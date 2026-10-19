using Anthill.Api.Filters;
using Anthill.Api.Requests;
using Anthill.Application.Common;
using Anthill.Application.DTOs.Imports;
using Anthill.Application.Interfaces.Services;
using Anthill.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anthill.Api.Controllers;

/// <summary>
/// Nhập học sinh, giáo viên và lớp từ file Excel — <c>/api/imports/*</c>.
/// </summary>
/// <remarks>
/// <para>
/// Mỗi kỳ có vài nghìn học sinh, không ai nhập tay từng em, nên quản trị viên tải file mẫu, điền, rồi nhập một lần. Chỉ
/// quản trị viên gọi được (giáo viên không tạo tài khoản).
/// </para>
/// <para>
/// <b>Hai bước trên cùng một endpoint.</b> <c>dryRun=true</c> kiểm tra và đếm mà không ghi gì, cho người dùng xem trước kết quả;
/// <c>dryRun=false</c> ghi thật và kiểm tra lại từ đầu (không tin kết quả xem trước). Dòng hỏng không làm hỏng cả mẻ: dòng
/// hợp lệ được nhập, dòng hỏng được liệt kê trong <c>issues</c> kèm số dòng và lý do.
/// </para>
/// <para>
/// File quá lớn bị chặn ngay từ <see cref="UploadSizeLimitAttribute"/>, trước khi ASP.NET Core đọc form.
/// </para>
/// </remarks>
[Authorize(Roles = SystemRoles.Admin)]
[Route("api/imports")]
public sealed class ImportsController : ApiControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IStudentImportService _students;
    private readonly ITeacherImportService _teachers;
    private readonly IGroupImportService _groups;
    private readonly ISchoolImportService _school;

    public ImportsController(
        IStudentImportService students,
        ITeacherImportService teachers,
        IGroupImportService groups,
        ISchoolImportService school)
    {
        _students = students;
        _teachers = teachers;
        _groups = groups;
        _school = school;
    }

    /// <summary>File mẫu nhập tổng hợp (.xlsx): một trang nhập liệu — mỗi dòng một học sinh kèm lớp và giáo viên chủ nhiệm.</summary>
    [HttpGet("school/template")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SchoolTemplate(CancellationToken cancellationToken)
        => Template(await _school.BuildTemplateAsync(cancellationToken), "mau-nhap-danh-sach-truong.xlsx");

    /// <summary>
    /// Nhập tổng hợp từ MỘT file: tạo giáo viên chủ nhiệm, tạo lớp (kèm chủ nhiệm), tạo học sinh và xếp vào lớp, trong một lần.
    /// </summary>
    /// <remarks>
    /// Mỗi dòng một học sinh, ghi kèm lớp và giáo viên chủ nhiệm. Cùng luật với ba bộ nhập riêng. Lỗi của giáo viên hay lớp thì các
    /// học sinh của lớp đó chưa được xếp. <c>dryRun=true</c> chạy hết rồi rollback (xem trước đúng như sẽ xảy ra).
    /// </remarks>
    [HttpPost("school")]
    [UploadSizeLimit(ImportLimits.MaxRequestBytes, ImportLimits.FileTooLargeMessage)]
    [ProducesResponseType<SchoolImportResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ImportSchool([FromForm] ImportSchoolForm form, CancellationToken cancellationToken)
    {
        if (form.File is null || form.File.Length == 0)
        {
            return FromFailure(Result.Failure("Chưa chọn file Excel để nhập."));
        }

        if (form.File.Length > ImportLimits.MaxFileBytes)
        {
            return FromFailure(Result.Failure(ImportLimits.FileTooLargeMessage));
        }

        await using var stream = form.File.OpenReadStream();

        return FromResult(await _school.ImportAsync(
            stream,
            new ImportSchoolOptions(form.SubjectId, form.Password, form.PublishGroups, form.DryRun),
            cancellationToken));
    }

    /// <summary>File mẫu nhập học sinh (.xlsx): trang nhập liệu có ô chọn lớp, và trang hướng dẫn.</summary>
    [HttpGet("students/template")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StudentsTemplate(CancellationToken cancellationToken)
        => Template(await _students.BuildTemplateAsync(cancellationToken), "mau-nhap-hoc-sinh.xlsx");

    /// <summary>
    /// Nhập học sinh: tạo tài khoản cho em chưa có và xếp lớp; em đã có (theo mã) chỉ được xếp lớp nếu chưa có lớp.
    /// </summary>
    [HttpPost("students")]
    [UploadSizeLimit(ImportLimits.MaxRequestBytes, ImportLimits.FileTooLargeMessage)]
    [ProducesResponseType<ImportResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> ImportStudents([FromForm] ImportStudentsForm form, CancellationToken cancellationToken)
        => Import(
            form.File,
            stream => _students.ImportAsync(
                stream,
                new ImportStudentsOptions(form.GroupId, form.Password, form.DryRun),
                cancellationToken));

    /// <summary>File mẫu nhập giáo viên (.xlsx): trang nhập liệu có ô chọn môn, và trang hướng dẫn.</summary>
    [HttpGet("teachers/template")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TeachersTemplate(CancellationToken cancellationToken)
        => Template(await _teachers.BuildTemplateAsync(cancellationToken), "mau-nhap-giao-vien.xlsx");

    /// <summary>Nhập giáo viên: tạo tài khoản (email là tên đăng nhập) kèm môn giảng dạy.</summary>
    [HttpPost("teachers")]
    [UploadSizeLimit(ImportLimits.MaxRequestBytes, ImportLimits.FileTooLargeMessage)]
    [ProducesResponseType<ImportResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> ImportTeachers([FromForm] ImportTeachersForm form, CancellationToken cancellationToken)
        => Import(
            form.File,
            stream => _teachers.ImportAsync(
                stream,
                new ImportTeachersOptions(form.SubjectId, form.Password, form.DryRun),
                cancellationToken));

    /// <summary>File mẫu nhập lớp (.xlsx): trang nhập liệu có ô chọn giáo viên, và trang hướng dẫn.</summary>
    [HttpGet("groups/template")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GroupsTemplate(CancellationToken cancellationToken)
        => Template(await _groups.BuildTemplateAsync(cancellationToken), "mau-nhap-lop.xlsx");

    /// <summary>Nhập lớp: lập các lớp mới (bản nháp), mỗi lớp kèm giáo viên phụ trách.</summary>
    [HttpPost("groups")]
    [UploadSizeLimit(ImportLimits.MaxRequestBytes, ImportLimits.FileTooLargeMessage)]
    [ProducesResponseType<ImportResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> ImportGroups([FromForm] ImportGroupsForm form, CancellationToken cancellationToken)
        => Import(
            form.File,
            stream => _groups.ImportAsync(stream, new ImportGroupsOptions(form.DryRun), cancellationToken));

    private IActionResult Template(Result<byte[]> result, string fileName) =>
        result.IsSuccess
            ? File(result.ValueOrThrow, XlsxContentType, fileName)
            : FromFailure(result);

    /// <summary>
    /// Kiểm file gửi lên rồi bóc <see cref="IFormFile"/> thành <see cref="Stream"/> — tầng Application không biết ASP.NET Core.
    /// </summary>
    private async Task<IActionResult> Import(IFormFile? file, Func<Stream, Task<Result<ImportResultDto>>> import)
    {
        if (file is null || file.Length == 0)
        {
            return FromFailure(Result.Failure("Chưa chọn file Excel để nhập."));
        }

        // Bộ lọc kích thước đã chặn từ ngoài; kiểm lại ở đây vì nó chỉ đếm cả thân request chứ không riêng file.
        if (file.Length > ImportLimits.MaxFileBytes)
        {
            return FromFailure(Result.Failure(ImportLimits.FileTooLargeMessage));
        }

        await using var stream = file.OpenReadStream();

        return FromResult(await import(stream));
    }
}
