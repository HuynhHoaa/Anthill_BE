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
            // điểm đó, có thể khác 100: chỉ bị bắt chỉnh lại khi giáo viên đổi cấu hình bản đề).
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
            // Quay về một điểm cho cả đề: giữ điểm mức Trung bình, điểm hai mức kia mất.
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
