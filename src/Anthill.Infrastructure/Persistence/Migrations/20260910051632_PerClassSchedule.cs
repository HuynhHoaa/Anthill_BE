using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerClassSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedViaGroupId",
                table: "ExamAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CloseAt",
                table: "ExamAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenAt",
                table: "ExamAssignments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedViaGroupId",
                table: "ExamAssignments");

            migrationBuilder.DropColumn(
                name: "CloseAt",
                table: "ExamAssignments");

            migrationBuilder.DropColumn(
                name: "OpenAt",
                table: "ExamAssignments");
        }
    }
}
