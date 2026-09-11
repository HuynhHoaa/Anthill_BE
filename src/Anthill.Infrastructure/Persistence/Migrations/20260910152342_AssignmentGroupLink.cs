using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssignmentGroupLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cột này ra đời không có khoá ngoại, nên có thể đã trỏ vào lớp bị xoá. Gỡ trước, nếu không
            // lệnh thêm khoá ngoại bên dưới hỏng ngay trên database đã có dữ liệu.
            migrationBuilder.Sql("""
                UPDATE "ExamAssignments" AS a SET "AssignedViaGroupId" = NULL
                WHERE a."AssignedViaGroupId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "StudentGroups" AS g WHERE g."Id" = a."AssignedViaGroupId");
                """);

            // Cùng lý do cho hai ràng buộc CHECK. Một cặp giờ lệch chỉ có thể do sửa tay mà có: nửa cặp thì
            // resolver vốn đã coi là "theo lịch chung", còn cặp đóng trước mở là một khung giờ không bao giờ
            // mở ra — không em nào làm được. Đưa cả hai về lịch chung là giá trị có nghĩa duy nhất.
            migrationBuilder.Sql("""
                UPDATE "ExamAssignments" SET "OpenAt" = NULL, "CloseAt" = NULL
                WHERE ("OpenAt" IS NULL) <> ("CloseAt" IS NULL)
                   OR "OpenAt" >= "CloseAt";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ExamAssignments_AssignedViaGroupId",
                table: "ExamAssignments",
                column: "AssignedViaGroupId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAssignments_ScheduleOrder",
                table: "ExamAssignments",
                sql: "\"OpenAt\" IS NULL OR \"OpenAt\" < \"CloseAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAssignments_SchedulePair",
                table: "ExamAssignments",
                sql: "(\"OpenAt\" IS NULL) = (\"CloseAt\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAssignments_StudentGroups_AssignedViaGroupId",
                table: "ExamAssignments",
                column: "AssignedViaGroupId",
                principalTable: "StudentGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamAssignments_StudentGroups_AssignedViaGroupId",
                table: "ExamAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ExamAssignments_AssignedViaGroupId",
                table: "ExamAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAssignments_ScheduleOrder",
                table: "ExamAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAssignments_SchedulePair",
                table: "ExamAssignments");
        }
    }
}
