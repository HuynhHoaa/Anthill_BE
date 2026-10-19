namespace Anthill.Application.DTOs.Questions;

/// <summary>
/// Một môn học, cho ô chọn môn của quản trị viên khi tạo câu hỏi và khi thu hẹp ngân hàng về một môn.
/// </summary>
public sealed record SubjectDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Code { get; init; }
}
