using Anthill.Application.Mappings;

namespace Anthill.Application.Common;

/// <summary>Kết luận của <see cref="AnswersReleaseGate.Evaluate"/>.</summary>
public enum AnswersReleaseGateState
{
    /// <summary>Đề chưa giao cho lớp nào: chưa có bài làm nào để mở đáp án cho.</summary>
    NoAssignments,

    /// <summary>Còn lớp đang làm bài, hoặc chưa tới giờ làm bài: chưa được mở.</summary>
    StillRunning,

    /// <summary>Lớp đóng bài muộn nhất đã hết giờ: mở được.</summary>
    TimeIsUp
}

/// <summary>
/// Giáo viên bấm "Mở đáp án" thì được hay chưa.
/// </summary>
/// <remarks>
/// <para>
/// Chỉ mở khi <b>hết giờ</b> — quyết định của chủ sản phẩm. Đề giao cho nhiều lớp, mỗi lớp một khung giờ;
/// lớp làm trước không được cầm đáp án chuyền cho lớp làm sau, nên "hết giờ" nghĩa là lớp đóng bài muộn
/// nhất (kể cả lớp chưa tới giờ làm) đã qua giờ đóng. Không có lối mở sớm.
/// </para>
/// <para>
/// So bằng "&gt;" như <c>ResolveAnswerVisibility</c>: đúng giây đóng bài thì lớp đó vẫn còn đang mở. Hàm thuần,
/// nhận mốc từ <c>IExamAssignmentRepository.GetAnswersReleaseAtAsync</c> để kiểm được bằng unit test.
/// </para>
/// </remarks>
public static class AnswersReleaseGate
{
    /// <param name="latestCloseUtc">
    /// Giờ đóng có hiệu lực muộn nhất trong mọi lượt giao của đề; <c>null</c> khi đề chưa giao cho ai.
    /// </param>
    /// <param name="nowUtc">Giờ server lúc bấm.</param>
    public static AnswersReleaseGateState Evaluate(DateTime? latestCloseUtc, DateTime nowUtc)
    {
        if (latestCloseUtc is not { } latestClose)
        {
            return AnswersReleaseGateState.NoAssignments;
        }

        return nowUtc.ToUtc() > latestClose.ToUtc()
            ? AnswersReleaseGateState.TimeIsUp
            : AnswersReleaseGateState.StillRunning;
    }
}
