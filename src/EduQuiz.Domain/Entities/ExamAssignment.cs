using EduQuiz.Domain.Common;

namespace EduQuiz.Domain.Entities;

/// <summary>
/// Việc giao một đề cho một học sinh. Duy nhất theo cặp (ExamId, StudentId) để
/// thao tác giao bài là idempotent.
/// </summary>
public class ExamAssignment : BaseEntity
{
    public int ExamId { get; set; }

    public int StudentId { get; set; }

    /// <summary>Mốc nghiệp vụ hiển thị cho giáo viên; <c>CreatedAt</c> ở lớp cơ sở là cột audit.</summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// Lớp mà em này được giao bài qua đó. NULL khi giáo viên chọn đích danh từng em.
    /// </summary>
    /// <remarks>
    /// Ghi lại tại thời điểm giao chứ không tra ngược qua <c>User.GroupId</c>: học sinh chuyển lớp
    /// giữa kỳ thì <c>GroupId</c> đổi theo, và lúc đó không còn cách nào biết bài này vốn được giao
    /// cho em với tư cách thành viên lớp nào — mà đó chính là thứ quyết định em làm bài vào khung
    /// giờ nào.
    ///
    /// Gửi lại một lớp để sửa khung giờ tra theo đúng cột này. Xoá lớp thì cột này về NULL (khoá
    /// ngoại SET NULL) nhưng khung giờ riêng vẫn ở lại: xoá hồ sơ một lớp không được phép dời giờ thi
    /// của các em.
    /// </remarks>
    public int? AssignedViaGroupId { get; set; }

    /// <summary>Lớp ứng với <see cref="AssignedViaGroupId"/>. Chỉ nạp khi cần hiện tên lớp.</summary>
    public StudentGroup? AssignedViaGroup { get; set; }

    /// <summary>
    /// Giờ mở bài riêng cho bản ghi này. NULL nghĩa là dùng lịch chung của đề.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Có mặt vì một đề thường được dùng cho nhiều lớp mà mỗi lớp học vào một tiết khác nhau. Giáo
    /// viên cho 12A1 làm tiết 2 sáng thứ Ba và 12A2 làm tiết 4 chiều thứ Năm, trên cùng một đề.
    /// </para>
    /// <para>
    /// Lịch nằm ở <b>bản ghi giao bài</b> chứ không phải ở một bảng riêng theo lớp, vì đây là nơi
    /// duy nhất biết đủ hai điều: em nào, và được giao theo điều kiện nào. Một bảng lịch theo lớp
    /// sẽ mất dấu ngay khi có em chuyển lớp.
    /// </para>
    /// <para>
    /// NULL chứ không phải chép sẵn lịch của đề vào: chép sẵn thì sửa lịch chung của đề không còn
    /// lan tới những lớp chưa đặt giờ riêng, mà đó lại là hành vi người dùng mong đợi.
    /// </para>
    /// </remarks>
    public DateTime? OpenAt { get; set; }

    /// <summary>
    /// Giờ đóng bài riêng. NULL nghĩa là dùng lịch chung của đề. Đi cùng cặp với <see cref="OpenAt"/>:
    /// chỉ đặt một nửa thì cả cặp bị bỏ qua và bản ghi theo trọn lịch chung.
    /// </summary>
    public DateTime? CloseAt { get; set; }

    public Exam Exam { get; set; } = null!;

    public User Student { get; set; } = null!;

    /// <summary>Tối đa một lượt làm bài — v1 không cho làm lại.</summary>
    public ExamAttempt? Attempt { get; set; }

    /// <summary>
    /// Đề riêng của học sinh này, chỉ tồn tại khi <c>Exam.RandomizePerStudent = true</c>.
    /// Ngược lại dùng đề chung của kỳ thi (ExamPaper có AssignmentId = NULL).
    /// </summary>
    public ExamPaper? Paper { get; set; }
}
