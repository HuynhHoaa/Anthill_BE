namespace EduQuiz.Application.DTOs.ExamAssignments;

/// <summary>Thân request của <c>POST /api/exams/{id}/assignments</c>.</summary>
/// <remarks>
/// Nhận cả học sinh lẻ lẫn cả nhóm trong một lần gọi, vì màn hình giao bài cho chọn xen kẽ hai
/// kiểu. Backend gộp và khử trùng lặp; con số frontend tự tính chỉ để hiện lên màn hình (§5).
/// </remarks>
public sealed record AssignExamRequest
{
    public IReadOnlyList<int> StudentIds { get; init; } = [];

    /// <summary>Giao cho toàn bộ học sinh của những nhóm này, tính tại thời điểm gọi.</summary>
    public IReadOnlyList<int> GroupIds { get; init; } = [];

    /// <summary>
    /// Bật: mỗi em một bản đề riêng. Tắt: cả lớp chung một bản đề.
    /// </summary>
    /// <remarks>
    /// Giá trị này được ghi lên <c>Exam</c> chứ không lên từng assignment, vì một đề mà nửa lớp
    /// dùng chung đề còn nửa kia mỗi em một đề thì không so sánh kết quả với nhau được nữa.
    /// Đổi cờ sau khi đã giao <b>không</b> sinh lại những bản đề đã phát.
    /// </remarks>
    /// <remarks>
    /// Nullable co chu dich: <c>null</c> nghia la "giu nguyen cai de dang co", con <c>false</c>
    /// nghia la "toi muon de chung".
    ///
    /// Kieu bool thuong thi hai y do khong phan biet duoc — bo trong truong nay va gui thang
    /// <c>false</c> deu toi server thanh <c>false</c>. Voi mot de da giao theo kieu de rieng, moi
    /// lan giao them du chi mot em cung bi tu choi 409, va cach duy nhat de them nguoi la thu hoi
    /// ca lop roi giao lai tu dau.
    /// </remarks>
    public bool? RandomizePerStudent { get; init; }

    /// <summary>Gửi thông báo cho những em vừa được giao. Chỉ áp dụng cho em <b>mới</b>, không nhắc lại.</summary>
    public bool Notify { get; init; } = true;

    /// <summary>
    /// Giờ mở bài riêng cho lượt giao này. Bỏ trống thì dùng lịch chung của đề.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Có mặt vì một đề thường được dùng cho nhiều lớp mà mỗi lớp học vào một tiết khác nhau: 12A1
    /// làm tiết 2 sáng thứ Ba, 12A2 làm tiết 4 chiều thứ Năm, trên cùng một đề. Không có trường
    /// này thì giáo viên phải nhân bản đề ra làm hai.
    /// </para>
    /// <para>
    /// Đi <b>cùng cặp</b> với <see cref="CloseAt"/>: gửi một nửa là trạng thái vô nghĩa và bị từ
    /// chối, thay vì ghép nửa gửi lên với nửa lấy từ đề.
    /// </para>
    /// </remarks>
    public DateTime? OpenAt { get; init; }

    /// <summary>Giờ đóng bài riêng. Đi cùng cặp với <see cref="OpenAt"/>.</summary>
    public DateTime? CloseAt { get; init; }
}
