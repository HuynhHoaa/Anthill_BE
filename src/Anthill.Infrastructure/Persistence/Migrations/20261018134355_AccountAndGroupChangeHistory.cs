using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Anthill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountAndGroupChangeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FromGroupId = table.Column<int>(type: "integer", nullable: true),
                    FromGroupName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ToGroupId = table.Column<int>(type: "integer", nullable: true),
                    ToGroupName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountChanges_Users_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountChanges_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GroupChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FromTeacherId = table.Column<int>(type: "integer", nullable: true),
                    FromTeacherName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ToTeacherId = table.Column<int>(type: "integer", nullable: true),
                    ToTeacherName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupChanges_StudentGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "StudentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupChanges_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountChanges_AccountId_CreatedAt",
                table: "AccountChanges",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountChanges_ActorUserId",
                table: "AccountChanges",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupChanges_ActorUserId",
                table: "GroupChanges",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupChanges_GroupId_CreatedAt",
                table: "GroupChanges",
                columns: new[] { "GroupId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountChanges");

            migrationBuilder.DropTable(
                name: "GroupChanges");
        }
    }
}
