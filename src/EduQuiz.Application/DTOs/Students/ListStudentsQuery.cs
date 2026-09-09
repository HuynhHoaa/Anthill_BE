using EduQuiz.Application.Common;

namespace EduQuiz.Application.DTOs.Students;

/// <summary>
/// Tham số của <c>GET /api/students</c>. Kế thừa <see cref="PaginationParams"/> nên
/// <c>page</c> và <c>pageSize</c> được kẹp ngay lúc model binder gán giá trị (§5).
/// </summary>
/// <remarks>
/// <para>
/// Frontend hiện lọc theo <b>tên</b> nhóm (<c>ListStudentsParams.group</c>) vì mock lưu tên nhóm
/// ngay trong bản ghi học sinh. Ở đây lọc theo <see cref="GroupId"/>: màn hình danh sách vốn đã
/// tải sẵn danh sách nhóm để đổ vào ô chọn, nên nó có id trong tay, và lọc theo khoá ngoại thì
/// đổi tên nhóm không làm bộ lọc trả về 0 dòng — đúng một trong chín mục mức CAO của
/// <c>FRONTEND_REVIEW.md</c>.
/// </para>
/// <para>
/// <see cref="Status"/> nhận chuỗi thay vì enum để <c>"all"</c> của frontend đi thẳng vào được.
/// Giá trị lạ bị từ chối chứ không âm thầm bỏ lọc — lọc sai mà vẫn trả 200 là kiểu lỗi người dùng
/// không bao giờ phát hiện ra.
/// </para>
/// </remarks>
public sealed class ListStudentsQuery : PaginationParams
{
    /// <summary>Từ khoá tìm trên họ tên, email, username và mã học sinh. Bỏ dấu, không phân biệt hoa thường (§5).</summary>
    public string? Search { get; set; }

    /// <summary>NULL là mọi nhóm; 0 là học sinh chưa thuộc nhóm nào.</summary>
    public int? GroupId { get; set; }

    /// <summary><c>"active"</c>, <c>"locked"</c>, hoặc <c>"all"</c>/bỏ trống để không lọc.</summary>
    public string? Status { get; set; }
}
