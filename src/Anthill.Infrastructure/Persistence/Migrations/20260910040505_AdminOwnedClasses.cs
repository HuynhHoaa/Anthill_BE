using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminOwnedClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentGroups_Users_OwnerTeacherId",
                table: "StudentGroups");

            migrationBuilder.RenameColumn(
                name: "OwnerTeacherId",
                table: "StudentGroups",
                newName: "CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_StudentGroups_OwnerTeacherId",
                table: "StudentGroups",
                newName: "IX_StudentGroups_CreatedByUserId");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "StudentGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "StudentGroups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudentGroupTeachers",
                columns: table => new
                {
                    StudentGroupId = table.Column<int>(type: "integer", nullable: false),
                    TeacherId = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentGroupTeachers", x => new { x.StudentGroupId, x.TeacherId });
                    table.ForeignKey(
                        name: "FK_StudentGroupTeachers_StudentGroups_StudentGroupId",
                        column: x => x.StudentGroupId,
                        principalTable: "StudentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentGroupTeachers_Users_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentGroups_IsPublished",
                table: "StudentGroups",
                column: "IsPublished");

            // Tên lớp trước đây chỉ duy nhất trong phạm vi một giáo viên, nên hai giáo viên đều có
            // thể có "Lớp 8A". Chỉ mục duy nhất sắp tạo sẽ làm migration đổ ngay ở những cơ sở dữ
            // liệu như vậy — đổi tên các bản trùng trước, giữ lại bản cũ nhất.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id",
                           "Name",
                           ROW_NUMBER() OVER (PARTITION BY lower("Name") ORDER BY "Id") AS rn
                    FROM "StudentGroups"
                )
                UPDATE "StudentGroups" g
                SET "Name" = left(r."Name", 120) || ' (' || r.rn || ')'
                FROM ranked r
                WHERE g."Id" = r."Id" AND r.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StudentGroups_Name",
                table: "StudentGroups",
                column: "Name",
                unique: true);

            // Mọi lớp đang có đều đang hoạt động trước khi đổi mô hình, nên chúng phải ở trạng thái
            // đã công bố. Để nguyên mặc định false là làm chúng biến mất khỏi màn hình của mọi giáo
            // viên ngay sau khi triển khai, mà không có lỗi nào báo lên.
            migrationBuilder.Sql("""
                UPDATE "StudentGroups"
                SET "IsPublished" = TRUE,
                    "PublishedAt" = COALESCE("PublishedAt", "CreatedAt", NOW())
                WHERE "IsPublished" = FALSE;
                """);

            // Chủ sở hữu cũ trở thành giáo viên được phân công. Không chuyển thì mọi lớp cũ mất
            // người phụ trách, và giáo viên đăng nhập vào thấy trống trơn dù dữ liệu vẫn còn nguyên.
            migrationBuilder.Sql("""
                INSERT INTO "StudentGroupTeachers" ("StudentGroupId", "TeacherId", "AssignedAt")
                SELECT g."Id", g."CreatedByUserId", NOW()
                FROM "StudentGroups" g
                JOIN "Users" u ON u."Id" = g."CreatedByUserId"
                WHERE u."Role" = 'Teacher'
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StudentGroupTeachers_TeacherId",
                table: "StudentGroupTeachers",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentGroups_Users_CreatedByUserId",
                table: "StudentGroups",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentGroups_Users_CreatedByUserId",
                table: "StudentGroups");

            migrationBuilder.DropTable(
                name: "StudentGroupTeachers");

            migrationBuilder.DropIndex(
                name: "IX_StudentGroups_IsPublished",
                table: "StudentGroups");

            migrationBuilder.DropIndex(
                name: "IX_StudentGroups_Name",
                table: "StudentGroups");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "StudentGroups");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "StudentGroups");

            migrationBuilder.RenameColumn(
                name: "CreatedByUserId",
                table: "StudentGroups",
                newName: "OwnerTeacherId");

            migrationBuilder.RenameIndex(
                name: "IX_StudentGroups_CreatedByUserId",
                table: "StudentGroups",
                newName: "IX_StudentGroups_OwnerTeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentGroups_Users_OwnerTeacherId",
                table: "StudentGroups",
                column: "OwnerTeacherId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
