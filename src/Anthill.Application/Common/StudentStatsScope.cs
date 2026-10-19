namespace Anthill.Application.Common;

/// <summary>
/// Những bài giao nào được tính vào số liệu của một học sinh (điểm trung bình, bài đã làm, bài đã giao) khi người xem là giáo viên:
/// bài giao qua các lớp giáo viên này đang dạy, cộng bài do chính giáo viên soạn.
/// </summary>
/// <remarks>
/// Một em học nhiều lớp nên không còn đúng khi số liệu gộp mọi bài của em: giáo viên Toán sẽ thấy một điểm trung bình trộn cả điểm môn
/// Anh của đồng nghiệp. Quản trị viên không có phạm vi (truyền <c>null</c>) và thấy tất cả.
/// </remarks>
public sealed record StudentStatsScope(IReadOnlyCollection<int> GroupIds, int TeacherId);
