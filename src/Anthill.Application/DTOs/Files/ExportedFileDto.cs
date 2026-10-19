namespace Anthill.Application.DTOs.Files;

/// <summary>
/// Một file do server dựng sẵn để người dùng tải về (bảng điểm .xlsx...). Tầng Application chỉ trao nội dung và tên file;
/// kiểu nội dung và tiêu đề tải về là việc của tầng Api.
/// </summary>
/// <param name="FileName">Tên file gợi ý, chỉ gồm chữ ASCII, số và dấu gạch để mọi trình duyệt lưu đúng tên.</param>
/// <param name="Content">Nội dung file.</param>
public sealed record ExportedFileDto(string FileName, byte[] Content);
