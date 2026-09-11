using Anthill.Application.Common;
using Anthill.Domain.Entities;
using FluentAssertions;

namespace Anthill.Tests.Attempts;

/// <summary>
/// Chấm điểm ở server, đối chiếu với bản chụp đề (§3 quy tắc ②).
/// </summary>
/// <remarks>
/// Đây là phép tính duy nhất quyết định điểm số của học sinh, nên nó phải đúng ở cả những tình
/// huống không ai gõ tay được trên giao diện: câu bỏ trống, lựa chọn trỏ vào một đáp án của câu
/// khác, đề rỗng. Mỗi ca dưới đây là một cách bài làm có thể tới server mà vẫn phải ra một con số
/// giải thích được.
/// </remarks>
public class AttemptGradingTests
{
    /// <summary>Một câu hai phương án: id lẻ là đáp án đúng, id chẵn là sai.</summary>
    private static ExamPaperQuestion Question(int id, decimal point = 1m)
    {
        var question = new ExamPaperQuestion
        {
            Id = id,
            Ordinal = id,
            ContentSnapshot = $"Câu {id}",
            Point = point
        };

        question.Answers.Add(new ExamPaperAnswer
        {
            Id = id * 10 + 1,
            ContentSnapshot = "Đúng",
            IsCorrect = true,
            Ordinal = 0
        });

        question.Answers.Add(new ExamPaperAnswer
        {
            Id = id * 10 + 2,
            ContentSnapshot = "Sai",
            IsCorrect = false,
            Ordinal = 1
        });

        return question;
    }

    private static int CorrectAnswerOf(int questionId) => questionId * 10 + 1;

    private static int WrongAnswerOf(int questionId) => questionId * 10 + 2;

    private static readonly List<ExamPaperQuestion> ThreeQuestions = [Question(1), Question(2), Question(3)];

    [Fact]
    public void Chon_dung_het_thi_duoc_tron_diem()
    {
        var selections = ThreeQuestions.ToDictionary(
            question => question.Id,
            question => (int?)CorrectAnswerOf(question.Id));

        var result = AttemptGrading.Grade(ThreeQuestions, selections);

        result.CorrectCount.Should().Be(3);
        result.AnsweredCount.Should().Be(3);
        result.Score.Should().Be(3m);
        result.MaxScore.Should().Be(3m);
    }

    [Fact]
    public void Chon_sai_het_thi_khong_diem_nhung_van_tinh_la_da_lam()
    {
        // Phân biệt "làm sai" với "bỏ trống" là việc của answeredCount: giáo viên nhìn một bài 0
        // điểm cần biết em có ngồi làm hay đã bỏ ngang từ câu đầu.
        var selections = ThreeQuestions.ToDictionary(
            question => question.Id,
            question => (int?)WrongAnswerOf(question.Id));

        var result = AttemptGrading.Grade(ThreeQuestions, selections);

        result.CorrectCount.Should().Be(0);
        result.AnsweredCount.Should().Be(3);
        result.Score.Should().Be(0m);
    }

    [Fact]
    public void Cau_bo_trong_khong_tinh_diem_va_khong_tinh_la_da_lam()
    {
        // Hai cách bỏ trống đều phải ra cùng kết quả: vắng mặt khỏi từ điển, và có mặt với giá trị
        // null (học sinh đã tích rồi bỏ tích).
        var selections = new Dictionary<int, int?>
        {
            [1] = CorrectAnswerOf(1),
            [2] = null
        };

        var result = AttemptGrading.Grade(ThreeQuestions, selections);

        result.CorrectCount.Should().Be(1);
        result.AnsweredCount.Should().Be(1);
        result.Score.Should().Be(1m);
        result.MaxScore.Should().Be(3m);
    }

    [Fact]
    public void Dap_an_khong_thuoc_cau_do_tinh_la_sai_chu_khong_nem_loi()
    {
        // Tầng service đã chặn từ lúc lưu nháp và trả 422, nhưng hàm chấm là chốt chặn cuối cùng —
        // nó không được phép tin rằng chốt chặn phía trước còn nguyên vẹn.
        var selections = new Dictionary<int, int?>
        {
            [1] = CorrectAnswerOf(2)
        };

        var result = AttemptGrading.Grade(ThreeQuestions, selections);

        result.CorrectCount.Should().Be(0);
        result.Score.Should().Be(0m);
    }

    [Fact]
    public void Lua_chon_tro_toi_cau_khong_co_trong_de_thi_bi_bo_qua()
    {
        // Bản đề bị sửa, hoặc client gửi một id bịa: điểm không được nhích lên vì một khoá lạ.
        var selections = new Dictionary<int, int?>
        {
            [999] = 9991
        };

        var result = AttemptGrading.Grade(ThreeQuestions, selections);

        result.CorrectCount.Should().Be(0);
        result.AnsweredCount.Should().Be(0);
        result.Score.Should().Be(0m);
    }

    [Fact]
    public void Diem_moi_cau_khac_nhau_thi_cong_theo_dung_cau_lam_dung()
    {
        var questions = new List<ExamPaperQuestion> { Question(1, 0.5m), Question(2, 2m), Question(3, 0.25m) };

        var selections = new Dictionary<int, int?>
        {
            [1] = CorrectAnswerOf(1),
            [2] = WrongAnswerOf(2),
            [3] = CorrectAnswerOf(3)
        };

        var result = AttemptGrading.Grade(questions, selections);

        result.CorrectCount.Should().Be(2);
        result.Score.Should().Be(0.75m);
        result.MaxScore.Should().Be(2.75m);
    }

    [Fact]
    public void Diem_le_duoc_lam_tron_hai_chu_so_dung_bang_cot_trong_database()
    {
        // Cột Score khai precision (6,2). Không làm tròn ở đây thì database làm thay, và con số
        // trả về cho học sinh ngay sau khi nộp sẽ lệch với con số đọc lại ở màn hình kết quả.
        var questions = new List<ExamPaperQuestion> { Question(1, 0.333m), Question(2, 0.333m) };

        var selections = new Dictionary<int, int?>
        {
            [1] = CorrectAnswerOf(1),
            [2] = CorrectAnswerOf(2)
        };

        var result = AttemptGrading.Grade(questions, selections);

        result.Score.Should().Be(0.67m);
    }

    [Fact]
    public void De_rong_thi_ra_khong_diem_chu_khong_no()
    {
        var result = AttemptGrading.Grade([], new Dictionary<int, int?>());

        result.CorrectCount.Should().Be(0);
        result.Score.Should().Be(0m);
        result.MaxScore.Should().Be(0m);
    }

    [Fact]
    public void Thang_diem_khong_phu_thuoc_hoc_sinh_lam_duoc_bao_nhieu()
    {
        // MaxScore chốt lúc bắt đầu làm bài, nên nó phải bằng nhau ở một bài trắng và một bài
        // đúng hết — nếu không, tỷ lệ phần trăm trên màn hình kết quả sẽ luôn là 100%.
        var blank = AttemptGrading.Grade(ThreeQuestions, new Dictionary<int, int?>());

        blank.MaxScore.Should().Be(3m);
        blank.MaxScore.Should().Be(AttemptGrading.MaxScoreOf(ThreeQuestions));
    }
}
