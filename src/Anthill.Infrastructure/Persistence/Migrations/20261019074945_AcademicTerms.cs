using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Anthill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AcademicTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcademicTermId",
                table: "StudentGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AcademicTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicTerms", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentGroups_AcademicTermId",
                table: "StudentGroups",
                column: "AcademicTermId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_IsCurrent",
                table: "AcademicTerms",
                column: "IsCurrent",
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_Name",
                table: "AcademicTerms",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentGroups_AcademicTerms_AcademicTermId",
                table: "StudentGroups",
                column: "AcademicTermId",
                principalTable: "AcademicTerms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentGroups_AcademicTerms_AcademicTermId",
                table: "StudentGroups");

            migrationBuilder.DropTable(
                name: "AcademicTerms");

            migrationBuilder.DropIndex(
                name: "IX_StudentGroups_AcademicTermId",
                table: "StudentGroups");

            migrationBuilder.DropColumn(
                name: "AcademicTermId",
                table: "StudentGroups");
        }
    }
}
