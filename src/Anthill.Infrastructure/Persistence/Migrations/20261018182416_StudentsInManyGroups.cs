using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentsInManyGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "StudentGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAt",
                table: "StudentGroups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudentGroupMembers",
                columns: table => new
                {
                    StudentGroupId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentGroupMembers", x => new { x.StudentGroupId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_StudentGroupMembers_StudentGroups_StudentGroupId",
                        column: x => x.StudentGroupId,
                        principalTable: "StudentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentGroupMembers_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentGroupMembers_StudentId",
                table: "StudentGroupMembers",
                column: "StudentId");

            // Mỗi em đang có lớp (Users.GroupId) trở thành một dòng thành viên. Không còn cách nào biết em vào lớp lúc nào nên
            // lấy mốc tạo tài khoản. Phải chép TRƯỚC khi bỏ cột bên dưới, nếu không mọi học sinh đang có lớp bị thả ra khỏi lớp.
            migrationBuilder.Sql(
                """
                INSERT INTO "StudentGroupMembers" ("StudentGroupId", "StudentId", "JoinedAt")
                SELECT "GroupId", "Id", "CreatedAt"
                FROM "Users"
                WHERE "GroupId" IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Users_StudentGroups_GroupId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_GroupId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "StudentGroups");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                table: "StudentGroups");

            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GroupId",
                table: "Users",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_StudentGroups_GroupId",
                table: "Users",
                column: "GroupId",
                principalTable: "StudentGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Mô hình cũ chỉ cho một lớp mỗi em: giữ lớp em vào gần nhất, các lớp còn lại mất khi lùi migration.
            migrationBuilder.Sql(
                """
                UPDATE "Users" u
                SET "GroupId" = m."StudentGroupId"
                FROM (
                    SELECT DISTINCT ON ("StudentId") "StudentId", "StudentGroupId"
                    FROM "StudentGroupMembers"
                    ORDER BY "StudentId", "JoinedAt" DESC, "StudentGroupId" DESC
                ) m
                WHERE u."Id" = m."StudentId";
                """);

            migrationBuilder.DropTable(
                name: "StudentGroupMembers");
        }
    }
}
