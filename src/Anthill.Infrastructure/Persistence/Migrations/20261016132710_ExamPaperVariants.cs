using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Anthill.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExamPaperVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaperCount",
                table: "Exams",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "VariantIndex",
                table: "ExamPapers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExamPaperVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamId = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    VariantIndex = table.Column<int>(type: "integer", nullable: false),
                    QuestionIds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamPaperVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamPaperVariants_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExamPaperVariants_StudentGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "StudentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamPaperVariants_Exam_Group_Variant",
                table: "ExamPaperVariants",
                columns: new[] { "ExamId", "GroupId", "VariantIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamPaperVariants_GroupId",
                table: "ExamPaperVariants",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamPaperVariants");

            migrationBuilder.DropColumn(
                name: "PaperCount",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "VariantIndex",
                table: "ExamPapers");
        }
    }
}
