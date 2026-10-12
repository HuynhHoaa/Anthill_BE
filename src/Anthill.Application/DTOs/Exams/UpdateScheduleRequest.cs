namespace Anthill.Application.DTOs.Exams;

/// <summary>
/// Thân request của <c>PATCH /api/exams/{id}/schedule</c> — chỉ đổi hai mốc thời gian.
/// </summary>
/// <remarks>
/// Tồn tại riêng thay vì bắt dùng PUT vì đây là thao tác giáo viên làm thường xuyên nhất sau khi
/// đề đã giao: dời lịch thi. Gửi PUT cho việc đó nghĩa là gửi lại trọn cấu hình, và chỉ cần một
/// trường bị bỏ sót ở phía client là tỷ lệ độ khó hoặc điểm từng mức bị ghi đè bằng giá trị mặc
/// định — trên một đề học sinh đang chờ làm.
/// </remarks>
public sealed record UpdateScheduleRequest
{
    public required DateTime OpenAt { get; init; }

    public required DateTime CloseAt { get; init; }
}
