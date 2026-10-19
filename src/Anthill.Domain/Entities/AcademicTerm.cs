using Anthill.Domain.Common;

namespace Anthill.Domain.Entities;

/// <summary>
/// Một kỳ học: đơn vị để gom các lớp theo thời gian học — "Học kỳ 1 · 2026–2027", "Kỳ 2 · 2026" ở đại học (một năm ba kỳ), hay "Lớp 11",
/// "Lớp 12" ở phổ thông (mỗi cấp lớp là một kỳ). Tên do quản trị viên đặt tự do nên dùng được cho cả hai kiểu trường.
/// </summary>
/// <remarks>
/// <para>
/// Kỳ chỉ là <b>nhãn gom lớp</b>: không đổi luật nào của lớp (khoá, chuyển lớp, giáo viên phụ trách, giao bài). Học sinh dùng nó để lọc
/// danh sách lớp và bảng điểm; lớp chưa gắn kỳ vẫn hoạt động bình thường và hiện ở mục "Chưa phân kỳ".
/// </para>
/// <para>
/// Tối đa một kỳ được đánh dấu <see cref="IsCurrent"/> (chỉ mục duy nhất có điều kiện ở database); giao diện học sinh mở sẵn kỳ đó.
/// Hết kỳ không có gì tự khoá: lớp của kỳ cũ vẫn mở như cũ cho tới khi quản trị viên khoá.
/// </para>
/// </remarks>
public class AcademicTerm : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Ngày bắt đầu (không bắt buộc); dùng để xếp kỳ mới lên trước.</summary>
    public DateOnly? StartsOn { get; set; }

    /// <summary>Ngày kết thúc (không bắt buộc).</summary>
    public DateOnly? EndsOn { get; set; }

    /// <summary>Kỳ hiện tại của nhà trường. Tối đa một kỳ.</summary>
    public bool IsCurrent { get; set; }

    public ICollection<StudentGroup> Groups { get; set; } = new List<StudentGroup>();
}
