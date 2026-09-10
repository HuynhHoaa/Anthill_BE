using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "Users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionRequestedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletionRequestedByUserId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeletionRequestedByUserId",
                table: "Users",
                column: "DeletionRequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Status_DeletionRequestedAt",
                table: "Users",
                columns: new[] { "Status", "DeletionRequestedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_DeletionRequestedByUserId",
                table: "Users",
                column: "DeletionRequestedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_DeletionRequestedByUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_DeletionRequestedByUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Status_DeletionRequestedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletionRequestedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletionRequestedByUserId",
                table: "Users");
        }
    }
}
