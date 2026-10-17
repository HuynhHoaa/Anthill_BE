using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExamPointsByDifficulty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Điểm một câu trước đây là một con số cho cả đề. Đổi tên cột đó thành điểm mức Trung bình rồi
            // chép sang hai mức còn lại, để đề cũ giữ đúng điểm từng câu như cũ (tổng điểm vẫn là số câu x
            // điểm đó, có thể khác 100: giáo viên chỉ bị hỏi xác nhận khi đổi cấu hình bản đề).
            migrationBuilder.RenameColumn(
                name: "PointPerQuestion",
                table: "Exams",
                newName: "PointMedium");

            migrationBuilder.AddColumn<decimal>(
                name: "PointEasy",
                table: "Exams",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PointHard",
                table: "Exams",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                "UPDATE \"Exams\" SET \"PointEasy\" = \"PointMedium\", \"PointHard\" = \"PointMedium\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Quay về một điểm cho cả đề: giữ điểm mức Trung bình, điểm hai mức kia mất. Mức không có câu nào
            // có điểm 0 (hợp lệ), mà một điểm duy nhất cho cả đề thì không được 0 khi đề còn câu: đề toàn câu
            // Dễ sẽ có PointMedium = 0 và mọi bản đề sau đó cho 0 điểm. Nên lấy điểm Trung bình nếu có, không
            // thì điểm Dễ, không thì điểm Khó.
            migrationBuilder.Sql(
                "UPDATE \"Exams\" SET \"PointMedium\" = COALESCE(NULLIF(\"PointMedium\", 0), "
                + "NULLIF(\"PointEasy\", 0), NULLIF(\"PointHard\", 0), 0) WHERE \"PointMedium\" = 0;");

            migrationBuilder.DropColumn(
                name: "PointEasy",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "PointHard",
                table: "Exams");

            migrationBuilder.RenameColumn(
                name: "PointMedium",
                table: "Exams",
                newName: "PointPerQuestion");
        }
    }
}
