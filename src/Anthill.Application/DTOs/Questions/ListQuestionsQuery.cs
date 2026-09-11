using Anthill.Application.Common;

namespace Anthill.Application.DTOs.Questions;

/// <summary>
/// Tham số của <c>GET /api/questions</c>. Kế thừa <see cref="PaginationParams"/> nên
/// <c>page</c> và <c>pageSize</c> được kẹp ngay lúc model binder gán giá trị (§5).
/// </summary>
/// <remarks>
/// Ba ô lọc nhận chuỗi thay vì enum để giá trị <c>"all"</c> của frontend đi thẳng vào được.
/// Giá trị lạ bị từ chối chứ không âm thầm bỏ lọc — trả về nhiều hơn người dùng yêu cầu mà vẫn
/// kèm mã 200 là kiểu sai không ai phát hiện.
/// </remarks>
public sealed class ListQuestionsQuery : PaginationParams
{
    /// <summary>Từ khoá tìm trên nội dung và chủ đề. Bỏ dấu, không phân biệt hoa thường (§5).</summary>
    public string? Search { get; set; }

    /// <summary>Tên chủ đề, hoặc <c>"all"</c>/bỏ trống để không lọc.</summary>
    public string? Topic { get; set; }

    /// <summary><c>"easy"</c>, <c>"medium"</c>, <c>"hard"</c>, hoặc <c>"all"</c>/bỏ trống.</summary>
    public string? Difficulty { get; set; }

    /// <summary><c>"active"</c>, <c>"hidden"</c>, hoặc <c>"all"</c>/bỏ trống.</summary>
    public string? Status { get; set; }

    /// <summary>
    /// Chỉ có tác dụng với <b>quản trị viên</b> — người không thuộc môn nào nên mặc định đọc toàn
    /// bộ ngân hàng; truyền vào để thu hẹp về một môn. Giáo viên gửi lên thì bỏ qua: phạm vi của
    /// họ do claim <c>subject_id</c> quyết định, không do tham số URL (§6).
    /// </summary>
    public int? SubjectId { get; set; }
}
