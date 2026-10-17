using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthill.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Câu hỏi cho chọn nhiều đáp án đúng: cờ trên câu hỏi và trên bản chụp câu của đề, và lựa chọn của học
    /// sinh chuyển từ một cột id sang một mảng id.
    /// </summary>
    public partial class MultipleAnswerQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowMultiple",
                table: "Questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowMultiple",
                table: "ExamPaperQuestions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int[]>(
                name: "SelectedAnswerIds",
                table: "StudentAnswers",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            // Chép trước khi bỏ cột cũ: mỗi lựa chọn đã lưu trở thành một mảng một phần tử. Dòng NULL
            // (em đã bỏ chọn) giữ mảng rỗng, đúng nghĩa "bỏ trống".
            migrationBuilder.Sql(
                "UPDATE \"StudentAnswers\" SET \"SelectedAnswerIds\" = ARRAY[\"SelectedAnswerId\"] "
                + "WHERE \"SelectedAnswerId\" IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentAnswers_ExamPaperAnswers_SelectedAnswerId",
                table: "StudentAnswers");

            migrationBuilder.DropIndex(
                name: "IX_StudentAnswers_SelectedAnswerId",
                table: "StudentAnswers");

            migrationBuilder.DropColumn(
                name: "SelectedAnswerId",
                table: "StudentAnswers");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Đường lui mất thông tin: câu nhiều đáp án đúng chỉ giữ lại phần tử đầu của tập đã chọn, và cờ
        /// <c>AllowMultiple</c> bị bỏ. Chỉ dùng để quay lại bản cũ ngay sau khi áp migration.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SelectedAnswerId",
                table: "StudentAnswers",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"StudentAnswers\" SET \"SelectedAnswerId\" = \"SelectedAnswerIds\"[1] "
                + "WHERE cardinality(\"SelectedAnswerIds\") > 0;");

            migrationBuilder.DropColumn(
                name: "SelectedAnswerIds",
                table: "StudentAnswers");

            migrationBuilder.DropColumn(
                name: "AllowMultiple",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "AllowMultiple",
                table: "ExamPaperQuestions");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnswers_SelectedAnswerId",
                table: "StudentAnswers",
                column: "SelectedAnswerId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentAnswers_ExamPaperAnswers_SelectedAnswerId",
                table: "StudentAnswers",
                column: "SelectedAnswerId",
                principalTable: "ExamPaperAnswers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
