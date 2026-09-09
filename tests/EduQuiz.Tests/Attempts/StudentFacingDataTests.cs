using System.Text.Json;
using System.Text.Json.Serialization;
using EduQuiz.Application.Mappings;
using EduQuiz.Domain.Entities;
using EduQuiz.Domain.Enums;
using FluentAssertions;

namespace EduQuiz.Tests.Attempts;

/// <summary>
/// Hai quy tắc bảo mật của §3 mà chỉ dữ liệu trả về mới chứng minh được: đáp án đúng không rời
/// server trong lúc làm bài (①) và quyền xem điểm thực thi ở server (⑥).
/// </summary>
/// <remarks>
/// Kiểm trên <b>chuỗi JSON thật</b> chứ không trên đối tượng, vì đó mới là thứ học sinh nhìn thấy
/// khi mở DevTools. Một trường <c>isCorrect</c> gán false vẫn qua được mọi phép kiểm trên đối
/// tượng, nhưng nằm nguyên trong response.
/// </remarks>
public class StudentFacingDataTests
{
    /// <summary>Đúng cấu hình serializer mà tầng Api đang dùng, kể cả quy tắc bỏ trường NULL.</summary>
    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static ExamPaperQuestion PaperQuestion()
    {
        var question = new ExamPaperQuestion
        {
            Id = 7,
            Ordinal = 0,
            ContentSnapshot = "Đạo hàm của x² là gì?",
            ImageUrlSnapshot = "/uploads/dao-ham.png",
            Point = 0.5m
        };

        question.Answers.Add(new ExamPaperAnswer
        {
            Id = 71,
            ContentSnapshot = "2x",
            IsCorrect = true,
            Ordinal = 0
        });

        question.Answers.Add(new ExamPaperAnswer
        {
            Id = 72,
            ContentSnapshot = "x",
            IsCorrect = false,
            Ordinal = 1
        });

        return question;
    }

    [Fact]
    public void Cau_hoi_tra_cho_hoc_sinh_khong_mang_theo_dap_an_dung()
    {
        var json = JsonSerializer.Serialize(PaperQuestion().ToAttemptQuestionDto(), ApiJson);

        // Không có trường isCorrect ở bất kỳ dạng viết nào, và cũng không có chữ "correct" nào lọt
        // ra dưới một cái tên khác.
        json.Should().NotContainEquivalentOf("correct");

        // Nhưng phần học sinh cần để làm bài thì vẫn đủ.
        json.Should().Contain("\"id\":7").And.Contain("2x").And.Contain("\"point\":0.5");
    }

    [Fact]
    public void Thu_tu_phuong_an_doc_theo_Ordinal_da_chot_luc_sinh_de()
    {
        var question = PaperQuestion();

        // Đảo thứ tự trong bộ nhớ để chứng minh hàm map sắp lại theo Ordinal chứ không theo thứ tự
        // navigation trả về — nếu không, mỗi lần tải lại trang học sinh thấy một thứ tự khác.
        var reversed = question.Answers.Reverse().ToList();
        question.Answers.Clear();
        foreach (var answer in reversed)
        {
            question.Answers.Add(answer);
        }

        question.ToAttemptQuestionDto().Answers.Select(answer => answer.Id).Should().Equal(71, 72);
    }

    private static ExamAssignment Assignment(bool showResultAfterSubmit, AssignmentStatus attemptStatus)
    {
        var exam = new Exam
        {
            Id = 3,
            Title = "Kiểm tra giữa kỳ",
            DurationMinutes = 45,
            TotalQuestions = 20,
            OpenAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc),
            CloseAt = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc),
            IsDraft = false,
            ShowResultAfterSubmit = showResultAfterSubmit,
            CreatedByUserId = 2,
            CreatedBy = new User
            {
                FullName = "Nguyễn Văn An",
                Email = "an.nv@eduquiz.vn",
                Username = "an.nv",
                PasswordHash = "hash",
                SecurityStamp = "stamp",
                AvatarColor = "#2563eb"
            }
        };

        return new ExamAssignment
        {
            Id = 11,
            ExamId = exam.Id,
            StudentId = 5,
            AssignedAt = new DateTime(2026, 9, 19, 7, 0, 0, DateTimeKind.Utc),
            Exam = exam,
            Attempt = new ExamAttempt
            {
                Id = 21,
                AssignmentId = 11,
                Status = attemptStatus,
                StartedAt = new DateTime(2026, 9, 20, 8, 5, 0, DateTimeKind.Utc),
                ExpiresAt = new DateTime(2026, 9, 20, 8, 50, 0, DateTimeKind.Utc),
                SubmittedAt = new DateTime(2026, 9, 20, 8, 40, 0, DateTimeKind.Utc),
                Score = 8.5m,
                MaxScore = 10m,
                CorrectCount = 17
            }
        };
    }

    [Fact]
    public void Bat_showResultAfterSubmit_thi_hoc_sinh_thay_diem()
    {
        var asOf = new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc);

        var item = Assignment(showResultAfterSubmit: true, AssignmentStatus.Graded).ToStudentExamItemDto(asOf);

        item.Score.Should().Be(8.5m);
        item.MaxScore.Should().Be(10m);
        item.CorrectCount.Should().Be(17);
        item.Status.Should().Be("graded");
    }

    [Fact]
    public void Tat_showResultAfterSubmit_thi_ba_truong_diem_vang_mat_khoi_JSON()
    {
        var asOf = new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc);

        var item = Assignment(showResultAfterSubmit: false, AssignmentStatus.Graded).ToStudentExamItemDto(asOf);
        var json = JsonSerializer.Serialize(item, ApiJson);

        // Ẩn ở giao diện là không đủ (§3 quy tắc ⑥): trường phải không có mặt trong response.
        json.Should().NotContain("\"score\"").And.NotContain("\"correctCount\"");
        item.ShowResultAfterSubmit.Should().BeFalse();
    }

    [Fact]
    public void Bai_chua_cham_thi_khong_co_diem_du_giao_vien_cho_xem_ket_qua()
    {
        var asOf = new DateTime(2026, 9, 20, 8, 30, 0, DateTimeKind.Utc);

        var item = Assignment(showResultAfterSubmit: true, AssignmentStatus.InProgress).ToStudentExamItemDto(asOf);

        item.Score.Should().BeNull();
        item.Status.Should().Be("inprogress");

        // Hạn giờ chỉ hiện với lượt đang chạy, để danh sách vẽ được đồng hồ đếm ngược.
        item.ExpiresAt.Should().Be(new DateTime(2026, 9, 20, 8, 50, 0, DateTimeKind.Utc));
        item.CanStart.Should().BeTrue();
    }

    [Fact]
    public void Da_nop_thi_khong_con_vao_thi_duoc_nua()
    {
        var asOf = new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc);

        var item = Assignment(showResultAfterSubmit: true, AssignmentStatus.Graded).ToStudentExamItemDto(asOf);

        item.CanStart.Should().BeFalse();
        item.ExpiresAt.Should().BeNull();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("all", null)]
    [InlineData("inprogress", AssignmentStatus.InProgress)]
    [InlineData("GRADED", AssignmentStatus.Graded)]
    public void Bo_loc_trang_thai_nhan_gia_tri_hop_le_va_coi_all_la_khong_loc(
        string? input,
        AssignmentStatus? expected)
    {
        ExamAttemptMappingExtensions.TryParseAttemptStatusFilter(input, out var parsed).Should().BeTrue();
        parsed.Should().Be(expected);
    }

    [Theory]
    [InlineData("done")]
    [InlineData("dang-lam")]
    [InlineData("42")]
    public void Bo_loc_trang_thai_tu_choi_gia_tri_la(string input)
    {
        // Bỏ lọc trong im lặng nghĩa là trả về nhiều hơn người dùng yêu cầu mà vẫn kèm mã 200.
        ExamAttemptMappingExtensions.TryParseAttemptStatusFilter(input, out var parsed).Should().BeFalse();
        parsed.Should().BeNull();
    }
}
