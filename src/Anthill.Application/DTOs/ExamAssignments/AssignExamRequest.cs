namespace Anthill.Application.DTOs.ExamAssignments;

/// <summary>Thân request của <c>POST /api/exams/{id}/assignments</c>.</summary>
/// <remarks>
/// <para>
/// Giao bài chỉ theo <b>lớp</b>: giáo viên chọn các lớp đã công bố và được phân công cho mình, mỗi lớp có thể
/// kèm khung giờ riêng. Cả lớp nhận bài; em vào lớp sau thì nhận bài ở lần giao kế tiếp (gửi lại lớp). Không
/// có đường giao riêng cho từng học sinh — nghiệp vụ không có khái niệm đó.
/// </para>
/// <para>
/// Backend gộp và khử trùng lặp; con số frontend tự tính chỉ để hiện lên màn hình (§5).
/// </para>
/// </remarks>
public sealed record AssignExamRequest
{
    /// <summary>
    /// <b>Đã bỏ.</b> Bài kiểm tra không giao riêng từng học sinh. Trường này chỉ còn để <b>từ chối rõ ràng</b>
    /// (400) một client cũ gửi danh sách em, thay vì lặng lẽ bỏ qua danh sách đó rồi báo "đã giao".
    /// </summary>
    public IReadOnlyList<int> StudentIds { get; init; } = [];

    /// <summary>Giao cho toàn bộ học sinh của những lớp này, tính tại thời điểm gọi.</summary>
    /// <remarks>
    /// Các lớp ở đây dùng cặp giờ chung <see cref="OpenAt"/>/<see cref="CloseAt"/> của request. Không kèm
    /// giờ thì khung giờ đã đặt cho lớp được giữ nguyên, và em mới vào lớp nhận đúng khung giờ lớp đang
    /// theo. Muốn mỗi lớp một khung giờ trong cùng một lần gọi thì dùng <see cref="Classes"/>; một lớp
    /// không được nằm ở cả hai danh sách.
    /// </remarks>
    public IReadOnlyList<int> GroupIds { get; init; } = [];

    /// <summary>Các lớp giao kèm khung giờ của riêng từng lớp — xem <see cref="AssignClassTarget"/>.</summary>
    public IReadOnlyList<AssignClassTarget> Classes { get; init; } = [];

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
    /// Giờ mở bài chung cho các lớp trong <see cref="GroupIds"/>. Bỏ trống thì không đụng tới khung giờ đã đặt
    /// cho lớp (lớp mới giao theo lịch chung của đề).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Có mặt vì một đề thường được dùng cho nhiều lớp mà mỗi lớp học vào một tiết khác nhau: 12A1
    /// làm tiết 2 sáng thứ Ba, 12A2 làm tiết 4 chiều thứ Năm, trên cùng một đề. Không có trường
    /// này thì giáo viên phải nhân bản đề ra làm hai.
    /// </para>
    /// <para>
    /// Đi <b>cùng cặp</b> với <see cref="CloseAt"/>: gửi một nửa là trạng thái vô nghĩa và bị từ
    /// chối, thay vì ghép nửa gửi lên với nửa lấy từ đề. Khung giờ phải chứa trọn thời lượng làm
    /// bài của đề, nếu không bị từ chối 400.
    /// </para>
    /// <para>
    /// Gửi lại một lớp đã giao kèm khung giờ thì khung giờ của những em được giao qua lớp đó mà chưa
    /// bắt đầu làm bài cũng được đổi theo; bỏ trống thì không đụng tới khung giờ đã đặt. Khung giờ đang
    /// khoá (còn dưới 120 phút nữa là mở cho tới hết giờ đóng) thì không đổi được: request trả 409.
    /// Cặp giờ này không áp cho các lớp trong <see cref="Classes"/> — mỗi lớp ở đó mang giờ của riêng nó.
    /// </para>
    /// </remarks>
    public DateTime? OpenAt { get; init; }

    /// <summary>Giờ đóng bài chung. Đi cùng cặp với <see cref="OpenAt"/>.</summary>
    public DateTime? CloseAt { get; init; }
}
